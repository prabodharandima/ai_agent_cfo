using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CfoAgent.Api.AI.Ollama;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Tests.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CfoAgent.Api.Tests.Health;

public sealed class OllamaOperationalTests
{
    [Fact]
    public async Task LiveHealth_DoesNotProbeOllamaWhenOllamaIsStopped()
    {
        var handler = new CountingHandler((_, _) => throw new HttpRequestException("unreachable local provider"));
        await using var factory = CreateFactory("Ollama", handler);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task ReadyHealth_DoesNotProbeOllamaWhenMockIsSelected()
    {
        var handler = new CountingHandler((_, _) => throw new HttpRequestException("must not be called"));
        await using var factory = CreateFactory("Mock", handler);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, handler.CallCount);
        Assert.Equal("Healthy", await GetDependencyStatusAsync(response, "ollama"));
    }

    [Fact]
    public async Task ReadyHealth_ReportsOllamaReadyWhenTheConfiguredModelIsAvailable()
    {
        var handler = new CountingHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/tags", request.RequestUri?.AbsolutePath);
            return Task.FromResult(JsonResponse("""{"models":[{"name":"llama3.2:3b"}]}"""));
        });
        await using var factory = CreateFactory("Ollama", handler);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("Healthy", await GetDependencyStatusAsync(response, "ollama"));
    }

    [Fact]
    public async Task ReadyHealth_ReportsSanitizedFailureWhenOllamaIsUnavailable()
    {
        var handler = new CountingHandler((_, _) => throw new HttpRequestException("http://private-provider:11434/provider-body"));
        await using var factory = CreateFactory("Ollama", handler);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await GetDependencyStatusAsync(response, "ollama"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Ollama is unavailable.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("private-provider", body, StringComparison.Ordinal);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ReadyHealth_ReportsTimeoutWhenOllamaDoesNotRespondWithinTheConfiguredLimit()
    {
        var handler = new CountingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse("unreachable");
        });
        await using var factory = CreateFactory("Ollama", handler, timeoutSeconds: "1");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Ollama health check timed out.", body, StringComparison.Ordinal);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task ChatApi_MapsOllamaProviderFailureToSanitizedProblemDetails()
    {
        await using var factory = CreateFactory("Ollama", new CountingHandler((_, _) => Task.FromResult(JsonResponse("""{"models":[{"name":"llama3.2:3b"}]}"""))), services =>
        {
            services.RemoveAll<IChatClient>();
            services.AddSingleton<IChatClient>(new ThrowingChatClient(new OllamaProviderException(OllamaFailureKind.Unavailable)));
        });
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/chat", new { message = "confidential finance prompt" });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable, body);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("selected model provider is temporarily unavailable", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confidential finance prompt", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatApi_WithOllamaConfigured_PreservesTheExistingResponseContract()
    {
        var handler = new CountingHandler(async (request, cancellationToken) =>
        {
            var requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var response = requestBody.Contains("[MOCK:CLASSIFY]", StringComparison.Ordinal)
                ? "SalesSummary"
                : "Grounded executive response based only on verified values.";
            return JsonResponse(CreateChatResponse(response));
        });
        await using var factory = CreateFactory("Ollama", handler);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/chat", new { message = "Give me the sales summary of this week." });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.Equal("sales_summary", body.GetProperty("responseType").GetString());
        Assert.Equal("Ollama", body.GetProperty("model").GetProperty("provider").GetString());
        Assert.Equal("llama3.2:3b", body.GetProperty("model").GetProperty("name").GetString());
        Assert.True(body.TryGetProperty("structuredData", out _));
        Assert.True(body.TryGetProperty("dataPeriod", out _));
        Assert.All(handler.RequestBodies, requestBody =>
        {
            using var request = JsonDocument.Parse(requestBody);
            Assert.False(request.RootElement.TryGetProperty("tools", out _));
        });
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string provider,
        HttpMessageHandler ollamaHandler,
        Action<IServiceCollection>? configureServices = null,
        string? timeoutSeconds = null) => new ChatApiFactory().WithWebHostBuilder(builder =>
    {
        builder.UseSetting("AI:Provider", provider);
        builder.UseSetting("AI:Model", "llama3.2:3b");
        if (timeoutSeconds is not null)
        {
            builder.UseSetting("AI:TimeoutSeconds", timeoutSeconds);
        }
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient(AiOptions.OllamaHttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => ollamaHandler);
            configureServices?.Invoke(services);
        });
    });

    private static async Task<string?> GetDependencyStatusAsync(HttpResponseMessage response, string dependencyName)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("dependencies").EnumerateArray()
            .Single(dependency => dependency.GetProperty("name").GetString() == dependencyName)
            .GetProperty("status")
            .GetString();
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static string CreateChatResponse(string content) => JsonSerializer.Serialize(new
    {
        model = "llama3.2:3b",
        created_at = "2026-07-18T00:00:00Z",
        message = new { role = "assistant", content },
        done = true,
        done_reason = "stop",
        total_duration = 1,
        load_duration = 1,
        prompt_eval_count = 1,
        prompt_eval_duration = 1,
        eval_count = 1,
        eval_duration = 1
    });

    private sealed class CountingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (request.Content is not null)
            {
                RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return await responseFactory(request, cancellationToken);
        }
    }

    private sealed class ThrowingChatClient(OllamaProviderException exception) : IChatClient
    {
        public ChatClientMetadata Metadata { get; } = new("Ollama", new Uri("http://localhost:11434"), "llama3.2:3b");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) => Task.FromException<ChatResponse>(exception);

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.FromException(exception);
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(ChatClientMetadata) ? Metadata : null;

        public void Dispose()
        {
        }
    }
}
