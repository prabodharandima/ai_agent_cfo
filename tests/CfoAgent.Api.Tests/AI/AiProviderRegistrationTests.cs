using System.Net;
using CfoAgent.Api.AI.Ollama;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Tests.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.AI;

public sealed class AiProviderRegistrationTests
{
    [Fact]
    public async Task DefaultConfiguration_SelectsAndRegistersTheOllamaChatClient()
    {
        await using var factory = CreateFactory(static _ => { });

        var chatClients = factory.Services.GetServices<IChatClient>().ToArray();

        Assert.Single(chatClients);
        Assert.IsType<OllamaChatClient>(chatClients[0]);
    }

    [Fact]
    public async Task DefaultConfiguration_BindsTheSelectedOllamaSettings()
    {
        await using var factory = CreateFactory(static _ => { });

        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var options = factory.Services.GetRequiredService<IOptions<AiOptions>>().Value;

        Assert.Equal("Ollama", configuration["AI:Provider"]);
        Assert.Equal("Ollama", options.Provider);
        Assert.Equal("llama3.2:3b", options.Ollama.Model);
        Assert.Equal("http://localhost:11434", options.Ollama.BaseUrl);
    }

    [Fact]
    public void ProductionAssembly_DoesNotContainMockChatClient()
    {
        var productionTypes = typeof(Program).Assembly.GetTypes();

        Assert.DoesNotContain(productionTypes, type =>
            string.Equals(type.Name, "MockChatClient", StringComparison.Ordinal)
            || string.Equals(type.Namespace, "CfoAgent.Api.AI.Mock", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OllamaRegistration_DoesNotSendAnHttpRequestDuringStartupOrResolution()
    {
        var requestCounter = new RequestCountingHandler();
        await using var factory = CreateFactory(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient(OllamaOptions.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => requestCounter);
            });
        });

        _ = factory.Services.GetRequiredService<IChatClient>();

        Assert.Equal(0, requestCounter.RequestCount);
    }

    [Fact]
    public async Task DependencyInjectionResolvesTheCompleteApplicationGraph()
    {
        await using var factory = new ChatApiFactory();
        using var scope = factory.Services.CreateScope();

        var orchestrator = scope.ServiceProvider.GetRequiredService<CfoOrchestratorAgent>();

        Assert.NotNull(orchestrator);
    }

    [Theory]
    [InlineData("AI:Provider", "Unsupported", "AI:Provider")]
    [InlineData("AI:Ollama:BaseUrl", "ftp://localhost:11434", "AI:Ollama:BaseUrl")]
    [InlineData("AI:Ollama:Model", "", "AI:Ollama:Model")]
    [InlineData("AI:Ollama:TimeoutSeconds", "0", "AI:Ollama:TimeoutSeconds")]
    [InlineData("AI:Ollama:ContextLength", "512", "AI:Ollama:ContextLength")]
    [InlineData("AI:Ollama:MaxOutputTokens", "0", "AI:Ollama:MaxOutputTokens")]
    [InlineData("AI:Ollama:Temperature", "2.1", "AI:Ollama:Temperature")]
    public async Task InvalidAiConfiguration_FailsPredictably(string key, string value, string expectedMessage)
    {
        await using var factory = CreateFactory(builder => builder.UseSetting(key, value));

        var exception = Assert.Throws<OptionsValidationException>(() => _ = factory.Services);

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(Action<IWebHostBuilder> configure) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            configure(builder);
        });

    private sealed class RequestCountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
