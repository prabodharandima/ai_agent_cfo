using System.Net;
using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.AI.Ollama;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Tests.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.AI;

public sealed class AiProviderRegistrationTests
{
    [Fact]
    public async Task DefaultConfiguration_RegistersOnlyTheMockChatClient()
    {
        await using var factory = new ChatApiFactory();

        var chatClients = factory.Services.GetServices<IChatClient>().ToArray();

        Assert.Single(chatClients);
        Assert.IsType<MockChatClient>(chatClients[0]);
    }

    [Fact]
    public async Task ValidOllamaConfiguration_RegistersOnlyTheOllamaChatClient()
    {
        await using var factory = CreateFactory(builder => ConfigureOllama(builder));

        var chatClients = factory.Services.GetServices<IChatClient>().ToArray();

        Assert.Single(chatClients);
        Assert.IsType<OllamaChatClient>(chatClients[0]);
    }

    [Fact]
    public async Task OllamaRegistration_DoesNotSendAnHttpRequestDuringStartupOrResolution()
    {
        var requestCounter = new RequestCountingHandler();
        await using var factory = CreateFactory(builder =>
        {
            ConfigureOllama(builder);
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient(AiOptions.OllamaHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => requestCounter);
            });
        });

        _ = factory.Services.GetRequiredService<IChatClient>();

        Assert.Equal(0, requestCounter.RequestCount);
    }

    [Theory]
    [InlineData("AI:Provider", "Unsupported", "AI:Provider")]
    [InlineData("AI:BaseUrl", "ftp://localhost:11434", "AI:BaseUrl")]
    [InlineData("AI:Model", "", "AI:Model")]
    [InlineData("AI:TimeoutSeconds", "0", "AI:TimeoutSeconds")]
    [InlineData("AI:ContextLength", "512", "AI:ContextLength")]
    [InlineData("AI:MaxOutputTokens", "0", "AI:MaxOutputTokens")]
    [InlineData("AI:Temperature", "2.1", "AI:Temperature")]
    public async Task InvalidAiConfiguration_FailsPredictably(string key, string value, string expectedMessage)
    {
        await using var factory = CreateFactory(builder => builder.UseSetting(key, value));

        var exception = Assert.Throws<OptionsValidationException>(() => _ = factory.Services);

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(Action<IWebHostBuilder> configure) =>
        new ChatApiFactory().WithWebHostBuilder(configure);

    private static void ConfigureOllama(IWebHostBuilder builder)
    {
        builder.UseSetting("AI:Provider", "Ollama");
        builder.UseSetting("AI:Model", "llama3.2:3b");
        builder.UseSetting("AI:BaseUrl", "http://localhost:11434");
        builder.UseSetting("AI:TimeoutSeconds", "120");
        builder.UseSetting("AI:Temperature", "0");
        builder.UseSetting("AI:ContextLength", "4096");
        builder.UseSetting("AI:MaxOutputTokens", "512");
    }

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
