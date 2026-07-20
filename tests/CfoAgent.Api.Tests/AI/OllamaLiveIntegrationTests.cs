using System.Net.Http.Json;
using System.Text.Json;
using CfoAgent.Api.AI;
using CfoAgent.Api.AI.Ollama;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Chroma;
using CfoAgent.Api.Rag.Embeddings;
using CfoAgent.Api.Rag.Retrieval;
using CfoAgent.Api.Tests.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Xunit;

namespace CfoAgent.Api.Tests.AI;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LiveOllamaCollection
{
    public const string Name = "LiveOllama";
}

[Collection(LiveOllamaCollection.Name)]
public sealed class OllamaLiveIntegrationTests
{
    [OllamaLiveFact]
    [Trait("Category", "LiveOllama")]
    public async Task EndpointAndConfiguredModel_AreAvailable()
    {
        var settings = LiveOllamaTestSettings.Load();
        using var client = CreateHttpClient(settings.Ai.Ollama);

        using var response = await client.GetAsync("api/tags");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        Assert.Equal("llama3.2:3b", settings.Ai.Ollama.Model, ignoreCase: true);
        Assert.Contains(document.RootElement.GetProperty("models").EnumerateArray(), model =>
            string.Equals(model.GetProperty("name").GetString(), settings.Ai.Ollama.Model, StringComparison.Ordinal));
    }

    [OllamaLiveFact]
    [Trait("Category", "LiveOllama")]
    public async Task ChatClient_CompletesAShortBoundedPromptWithConfiguredMetadata()
    {
        var settings = LiveOllamaTestSettings.Load();
        using var httpClient = CreateHttpClient(settings.Ai.Ollama);
        using var chatClient = CreateChatClient(httpClient, settings.Ai.Ollama);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Reply with a short greeting for a CFO assistant.")],
            cancellationToken: timeout.Token);

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
        Assert.Equal("Ollama", chatClient.Metadata.ProviderName);
        Assert.Equal(settings.Ai.Ollama.Model, chatClient.Metadata.DefaultModelId);
    }

    [OllamaLiveFact]
    [Trait("Category", "LiveOllama")]
    public async Task ChatApi_SalesSummaryPreservesDeterministicStructuredData()
    {
        var settings = LiveOllamaTestSettings.Load();
        await using var factory = CreateChatApiFactory(settings.Ai.Ollama);
        using var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(90);

        using var response = await client.PostAsJsonAsync("/api/chat", new
        {
            message = "Give me the sales summary of this week."
        });

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        var body = document.RootElement;
        Assert.Equal("sales_summary", body.GetProperty("responseType").GetString());
        Assert.Equal("Ollama", body.GetProperty("model").GetProperty("provider").GetString());
        Assert.Equal(settings.Ai.Ollama.Model, body.GetProperty("model").GetProperty("name").GetString());
        Assert.True(body.GetProperty("structuredData").TryGetProperty("NetRevenue", out _));
    }

    [OllamaLiveKnowledgeFact]
    [Trait("Category", "LiveOllama")]
    public async Task FinancialKnowledgeAgent_RetainsIndexedSourcesWhenLocalChromaDataIsAvailable()
    {
        var settings = LiveOllamaTestSettings.Load();
        using var ollamaHttpClient = CreateHttpClient(settings.Ai.Ollama);
        using var chatClient = CreateChatClient(ollamaHttpClient, settings.Ai.Ollama);
        using var chromaHttpClient = CreateHttpClient(settings.Chroma.BaseUrl, settings.Chroma.TimeoutSeconds);
        using var services = new ServiceCollection().BuildServiceProvider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        var chromaClient = new ChromaClient(chromaHttpClient, Options.Create(settings.Chroma));
        IFinancialKnowledgeSearch retrievalService = new ChromaFinancialKnowledgeSearch(
            chromaClient,
            new DeterministicTokenHashEmbeddingGenerator(),
            Options.Create(settings.Rag));
        var agent = new FinancialKnowledgeAgent(retrievalService, chatClient, Options.Create(settings.Rag));

        var result = await agent.AnswerAsync(
            new AgentRequest("What is the annual sales target and what assumptions were used?"),
            cancellationToken: timeout.Token);

        Assert.Equal(AgentResponseType.Knowledge, result.ResponseType);
        Assert.False(string.IsNullOrWhiteSpace(result.Answer));
        Assert.NotEmpty(result.Sources);
    }

    private static HttpClient CreateHttpClient(OllamaOptions options) =>
        CreateHttpClient(options.BaseUrl, options.TimeoutSeconds);

    private static HttpClient CreateHttpClient(string baseUrl, int timeoutSeconds) => new()
    {
        BaseAddress = new Uri(baseUrl),
        Timeout = TimeSpan.FromSeconds(timeoutSeconds)
    };

    private static OllamaChatClient CreateChatClient(HttpClient httpClient, OllamaOptions options) =>
        new((IChatClient)new OllamaApiClient(httpClient, options.Model), options, new AiProviderDescriptor("Ollama", options.Model));

    private static WebApplicationFactory<Program> CreateChatApiFactory(OllamaOptions options) =>
        new ChatApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AI:Provider", "Ollama");
            builder.UseSetting("AI:Ollama:Model", options.Model);
            builder.UseSetting("AI:Ollama:BaseUrl", options.BaseUrl);
            builder.UseSetting("AI:Ollama:TimeoutSeconds", options.TimeoutSeconds.ToString());
            builder.UseSetting("AI:Ollama:Temperature", options.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("AI:Ollama:ContextLength", options.ContextLength.ToString());
            builder.UseSetting("AI:Ollama:MaxOutputTokens", options.MaxOutputTokens.ToString());
        });
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OllamaLiveFactAttribute : FactAttribute
{
    public OllamaLiveFactAttribute()
    {
        Skip = OllamaLiveAvailability.Value.SkipReason;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OllamaLiveKnowledgeFactAttribute : FactAttribute
{
    public OllamaLiveKnowledgeFactAttribute()
    {
        Skip = OllamaLiveAvailability.Value.SkipReason ?? LocalChromaKnowledgeAvailability.Value;
    }
}

internal static class OllamaLiveAvailability
{
    private const string OptInVariable = "CFO_AGENT_RUN_OLLAMA_TESTS";
    private static readonly Lazy<LiveOllamaAvailability> Availability = new(Probe);

    public static LiveOllamaAvailability Value => Availability.Value;

    private static LiveOllamaAvailability Probe()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "true", StringComparison.OrdinalIgnoreCase))
        {
            return new LiveOllamaAvailability($"Set {OptInVariable}=true to run local Ollama tests.", null);
        }

        LiveOllamaTestSettings settings;
        try
        {
            settings = LiveOllamaTestSettings.Load();
        }
        catch (Exception)
        {
            return new LiveOllamaAvailability("Live Ollama test configuration is invalid. Set AI__Ollama__BaseUrl and AI__Ollama__Model before opting in.", null);
        }

        if (!string.Equals(settings.Ai.Ollama.Model, "llama3.2:3b", StringComparison.OrdinalIgnoreCase))
        {
            return new LiveOllamaAvailability("Live Ollama tests require AI__Ollama__Model=llama3.2:3b.", settings);
        }

        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(settings.Ai.Ollama.BaseUrl),
                Timeout = TimeSpan.FromSeconds(5)
            };
            using var response = client.GetAsync("api/tags").GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return new LiveOllamaAvailability("Local Ollama did not return a successful tags response. Start Ollama and retry.", settings);
            }

            using var document = JsonDocument.Parse(response.Content.ReadAsStream());
            var modelAvailable = document.RootElement.TryGetProperty("models", out var models)
                && models.ValueKind == JsonValueKind.Array
                && models.EnumerateArray().Any(model =>
                    model.TryGetProperty("name", out var name)
                    && string.Equals(name.GetString(), settings.Ai.Ollama.Model, StringComparison.Ordinal));
            return modelAvailable
                ? new LiveOllamaAvailability(null, settings)
                : new LiveOllamaAvailability("The configured llama3.2:3b model is unavailable. Install it manually with 'ollama pull llama3.2:3b' and retry.", settings);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException or JsonException)
        {
            return new LiveOllamaAvailability("Local Ollama is unavailable. Start the configured endpoint and retry; the test does not download models automatically.", settings);
        }
    }
}

internal static class LocalChromaKnowledgeAvailability
{
    private static readonly Lazy<string?> SkipReason = new(Probe);

    public static string? Value => SkipReason.Value;

    private static string? Probe()
    {
        var settings = OllamaLiveAvailability.Value.Settings;
        if (settings is null)
        {
            return "Local Chroma knowledge validation requires available Ollama live-test settings.";
        }

        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(settings.Chroma.BaseUrl),
                Timeout = TimeSpan.FromSeconds(5)
            };
            using var heartbeat = client.GetAsync("api/v2/heartbeat").GetAwaiter().GetResult();
            if (!heartbeat.IsSuccessStatusCode)
            {
                return "Local ChromaDB is unavailable. Start Docker Compose and ingest the knowledge documents to run this test.";
            }

            var collectionPath = $"api/v2/tenants/{Uri.EscapeDataString(settings.Chroma.Tenant)}/databases/{Uri.EscapeDataString(settings.Chroma.Database)}/collections/{Uri.EscapeDataString(settings.Chroma.CollectionName)}";
            using var collection = client.GetAsync(collectionPath).GetAwaiter().GetResult();
            return collection.IsSuccessStatusCode
                ? null
                : "The local ChromaDB knowledge collection is unavailable. Ingest the knowledge documents before running this test.";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            return "Local ChromaDB is unavailable. Start Docker Compose and ingest the knowledge documents to run this test.";
        }
    }
}

internal sealed record LiveOllamaAvailability(string? SkipReason, LiveOllamaTestSettings? Settings);

internal sealed record LiveOllamaTestSettings(AiOptions Ai, ChromaOptions Chroma, RagOptions Rag)
{
    public static LiveOllamaTestSettings Load()
    {
        var root = FindRepositoryRoot();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(root)
            .AddJsonFile("src/CfoAgent.Api/appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();
        var ai = configuration.GetRequiredSection(AiOptions.SectionName).Get<AiOptions>()
            ?? throw new InvalidOperationException("AI configuration is required.");
        var chroma = configuration.GetRequiredSection(ChromaOptions.SectionName).Get<ChromaOptions>()
            ?? throw new InvalidOperationException("Chroma configuration is required.");
        var rag = configuration.GetRequiredSection(RagOptions.SectionName).Get<RagOptions>()
            ?? throw new InvalidOperationException("RAG configuration is required.");
        return new LiveOllamaTestSettings(ai, chroma, rag);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CfoAgent.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the CFO AI Agent repository root.");
    }
}
