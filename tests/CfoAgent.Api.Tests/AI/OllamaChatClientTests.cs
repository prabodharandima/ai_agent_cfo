using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using CfoAgent.Api.AI;
using CfoAgent.Api.AI.Ollama;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace CfoAgent.Api.Tests.AI;

public sealed class OllamaChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_UsesConfiguredModelAndBoundedGenerationOptions()
    {
        var handler = new RecordingHandler(_ => JsonResponse(CreateChatResponse("Grounded executive summary.")));
        using var fixture = CreateClient(handler);

        var response = await fixture.Client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Use only the supplied verified values.")]);

        Assert.Equal("Grounded executive summary.", response.Text);
        Assert.NotNull(handler.RequestBody);
        using var request = JsonDocument.Parse(handler.RequestBody);
        var root = request.RootElement;
        Assert.Equal("configured-model", root.GetProperty("model").GetString());
        Assert.Equal("Use only the supplied verified values.", root.GetProperty("messages")[0].GetProperty("content").GetString());
        var generationOptions = root.GetProperty("options");
        Assert.Equal(0.25, generationOptions.GetProperty("temperature").GetDouble(), precision: 2);
        Assert.Equal(4096, generationOptions.GetProperty("num_ctx").GetInt32());
        Assert.Equal(256, generationOptions.GetProperty("num_predict").GetInt32());
        Assert.False(root.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task GetResponseAsync_ForwardsExplicitlyApprovedTools()
    {
        var handler = new RecordingHandler(_ => JsonResponse(CreateChatResponse("Tool selection received.")));
        using var fixture = CreateClient(handler);
        var tool = AIFunctionFactory.Create(
            (string relativePath) => relativePath,
            "read_knowledge_file",
            "Read one approved knowledge file.");

        await fixture.Client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Select a supplied tool.")],
            new ChatOptions { Tools = [tool], ToolMode = ChatToolMode.RequireAny });

        Assert.NotNull(handler.RequestBody);
        using var request = JsonDocument.Parse(handler.RequestBody);
        var serializedTool = Assert.Single(request.RootElement.GetProperty("tools").EnumerateArray());
        Assert.Equal("read_knowledge_file", serializedTool.GetProperty("function").GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetResponseAsync_AcceptsStructuredToolCallWithoutProse()
    {
        var handler = new RecordingHandler(_ => JsonResponse(CreateToolCallResponse()));
        using var fixture = CreateClient(handler);
        var tool = AIFunctionFactory.Create(
            (string relativePath) => relativePath,
            "read_knowledge_file",
            "Read one approved knowledge file.");

        var response = await fixture.Client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Read the file.")],
            new ChatOptions { Tools = [tool], ToolMode = ChatToolMode.RequireAny });

        var call = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>());
        Assert.Equal("read_knowledge_file", call.Name);
        Assert.Equal("budget.md", call.Arguments?["relativePath"]?.ToString());
    }

    [Fact]
    public async Task GetResponseAsync_SerializesTheConfiguredLlamaModel()
    {
        const string model = "llama3.2:3b";
        var handler = new RecordingHandler(_ => JsonResponse(CreateChatResponse("Grounded executive summary.", model)));
        using var fixture = CreateClient(handler, model: model);

        await fixture.Client.GetResponseAsync([new ChatMessage(ChatRole.User, "Use verified values only.")]);

        Assert.NotNull(handler.RequestBody);
        using var request = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal(model, request.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public void Metadata_ReportsOllamaEndpointAndConfiguredModel()
    {
        using var fixture = CreateClient(new RecordingHandler(_ => JsonResponse(CreateChatResponse("unused"))));

        var metadata = Assert.IsType<ChatClientMetadata>(fixture.Client.GetService(typeof(ChatClientMetadata)));

        Assert.Equal("Ollama", metadata.ProviderName);
        Assert.Equal(new Uri("http://localhost:11434"), metadata.ProviderUri);
        Assert.Equal("configured-model", metadata.DefaultModelId);
    }

    [Fact]
    public async Task GetResponseAsync_PropagatesCallerCancellation()
    {
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(CreateChatResponse("unreachable"));
        });
        using var fixture = CreateClient(handler);
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "cancel")], cancellationToken: cancellationSource.Token));
    }

    [Fact]
    public async Task GetResponseAsync_ConvertsConfiguredTimeoutToControlledFailure()
    {
        var handler = new RecordingHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(CreateChatResponse("unreachable"));
        });
        using var fixture = CreateClient(handler, timeoutSeconds: 1);

        var exception = await Assert.ThrowsAsync<AiProviderException>(() => fixture.Client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "timeout")]));

        Assert.Equal(AiProviderFailureKind.Timeout, exception.FailureKind);
        Assert.Equal("The model provider request timed out.", exception.Message);
    }

    [Fact]
    public async Task GetResponseAsync_SanitizesNonSuccessResponses()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("sensitive provider response", Encoding.UTF8, "text/plain")
        });
        using var fixture = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(() => fixture.Client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "private prompt")]));

        Assert.Equal(AiProviderFailureKind.Unavailable, exception.FailureKind);
        Assert.DoesNotContain("sensitive", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private prompt", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetResponseAsync_ConvertsTransportFailuresToControlledUnavailableFailure()
    {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("sensitive endpoint details"));
        using var fixture = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(() => fixture.Client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "private prompt")]));

        Assert.Equal(AiProviderFailureKind.Unavailable, exception.FailureKind);
        Assert.Equal("The model provider is unavailable.", exception.Message);
    }

    [Fact]
    public async Task GetResponseAsync_SanitizesMalformedJsonResponses()
    {
        var handler = new RecordingHandler(_ => JsonResponse("not valid json"));
        using var fixture = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(() => fixture.Client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "private prompt")]));

        Assert.Equal(AiProviderFailureKind.InvalidResponse, exception.FailureKind);
        Assert.Equal("The model provider returned an invalid response.", exception.Message);
    }

    [Fact]
    public async Task GetResponseAsync_RejectsAnEmptyCompletion()
    {
        var handler = new RecordingHandler(_ => JsonResponse(CreateChatResponse("   ")));
        using var fixture = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AiProviderException>(() => fixture.Client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "empty")]));

        Assert.Equal(AiProviderFailureKind.InvalidResponse, exception.FailureKind);
    }

    [Fact]
    public async Task GetResponseAsync_LogsOnlySafeOperationalMetadata()
    {
        var prompt = "confidential prompt that must not be logged";
        var providerBody = "confidential provider response that must not be logged";
        var logger = new RecordingLogger<OllamaChatClient>();
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(providerBody, Encoding.UTF8, "text/plain")
        });
        using var fixture = CreateClient(handler, logger: logger);

        await Assert.ThrowsAsync<AiProviderException>(() => fixture.Client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)]));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal("Ollama", entry["Provider"]);
        Assert.Equal("configured-model", entry["Model"]);
        Assert.Equal("chat", entry["Operation"]);
        Assert.Equal("Failure", entry["Outcome"]);
        Assert.Equal("Unavailable", entry["FailureCategory"]);
        Assert.True(Assert.IsType<long>(entry["DurationMilliseconds"]) >= 0);
        Assert.DoesNotContain(prompt, entry.FormattedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(providerBody, entry.FormattedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("localhost", entry.FormattedMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_UsesTheUnderlyingStreamingTransport()
    {
        using var transport = new StreamingTransport("Verified ", "streamed response.");
        using var client = CreateStreamingClient(transport);
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Use only verified values.")]))
        {
            updates.Add(update);
        }

        Assert.Equal(1, transport.StreamCallCount);
        Assert.Equal(0, transport.ResponseCallCount);
        Assert.Equal("Verified streamed response.", string.Concat(updates.Select(update => update.Text)));
    }

    private static ClientFixture CreateClient(
        HttpMessageHandler handler,
        int timeoutSeconds = 5,
        ILogger<OllamaChatClient>? logger = null,
        string model = "configured-model")
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromSeconds(timeoutSeconds + 1)
        };
        var options = new OllamaOptions
        {
            Model = model,
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = timeoutSeconds,
            Temperature = 0.25,
            ContextLength = 4096,
            MaxOutputTokens = 256
        };
        var transport = (IChatClient)new OllamaApiClient(httpClient, options.Model);
        return new ClientFixture(httpClient, new OllamaChatClient(
            transport,
            options,
            new AiProviderDescriptor("Ollama", model),
            logger));
    }

    private static OllamaChatClient CreateStreamingClient(IChatClient transport)
    {
        var options = new OllamaOptions
        {
            Model = "configured-model",
            BaseUrl = "http://localhost:11434",
            TimeoutSeconds = 5,
            Temperature = 0.25,
            ContextLength = 4096,
            MaxOutputTokens = 256
        };

        return new OllamaChatClient(transport, options, new AiProviderDescriptor("Ollama", options.Model));
    }

    private static string CreateChatResponse(string content, string model = "configured-model") => JsonSerializer.Serialize(new
    {
        model,
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

    private static string CreateToolCallResponse() => JsonSerializer.Serialize(new
    {
        model = "configured-model",
        created_at = "2026-07-18T00:00:00Z",
        message = new
        {
            role = "assistant",
            content = string.Empty,
            tool_calls = new[]
            {
                new
                {
                    function = new
                    {
                        name = "read_knowledge_file",
                        arguments = new { relativePath = "budget.md" }
                    }
                }
            }
        },
        done = true,
        done_reason = "stop",
        total_duration = 1,
        load_duration = 1,
        prompt_eval_count = 1,
        prompt_eval_duration = 1,
        eval_count = 0,
        eval_duration = 0
    });

    private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class ClientFixture(HttpClient httpClient, OllamaChatClient client) : IDisposable
    {
        public OllamaChatClient Client { get; } = client;

        public void Dispose()
        {
            Client.Dispose();
            httpClient.Dispose();
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            : this((request, _) => Task.FromResult(responseFactory(request)))
        {
        }

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            this.responseFactory = responseFactory;
        }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return await responseFactory(request, cancellationToken);
        }
    }

    private sealed class StreamingTransport(params string[] updates) : IChatClient
    {
        public ChatClientMetadata Metadata { get; } = new("Test", new Uri("https://test.local"), "test-model");

        public int ResponseCallCount { get; private set; }

        public int StreamCallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ResponseCallCount++;
            throw new InvalidOperationException("The completed-response transport should not be called for streaming.");
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamCallCount++;
            foreach (var update in updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new ChatResponseUpdate(ChatRole.Assistant, update)
                {
                    ModelId = Metadata.DefaultModelId
                };
                await Task.Yield();
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(ChatClientMetadata) ? Metadata : null;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> structuredState
                ? structuredState.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>();
            Entries.Add(new LogEntry(values, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(IReadOnlyDictionary<string, object?> Values, string FormattedMessage)
    {
        public object? this[string key] => Values[key];
    }
}
