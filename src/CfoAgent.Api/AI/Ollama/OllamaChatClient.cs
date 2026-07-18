using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Exceptions;

namespace CfoAgent.Api.AI.Ollama;

public sealed class OllamaChatClient : IChatClient
{
    private readonly IChatClient transport;
    private readonly AiOptions options;
    private readonly ILogger<OllamaChatClient> logger;

    public OllamaChatClient(
        IChatClient transport,
        AiOptions options,
        ILogger<OllamaChatClient>? logger = null)
    {
        this.transport = transport;
        this.options = options;
        this.logger = logger ?? NullLogger<OllamaChatClient>.Instance;
        Metadata = new ChatClientMetadata("Ollama", new Uri(options.BaseUrl), options.Model);
    }

    public ChatClientMetadata Metadata { get; }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var requestOptions = CreateRequestOptions(options);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(this.options.TimeoutSeconds));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await transport.GetResponseAsync(messages, requestOptions, timeoutSource.Token);
            if (string.IsNullOrWhiteSpace(response.Text))
            {
                throw new OllamaProviderException(OllamaFailureKind.InvalidResponse);
            }

            LogOutcome(stopwatch, "Success", "None");
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogOutcome(stopwatch, "Cancelled", "CallerCancelled");
            throw;
        }
        catch (OperationCanceledException)
        {
            LogOutcome(stopwatch, "Failure", "Timeout");
            throw new OllamaProviderException(OllamaFailureKind.Timeout);
        }
        catch (HttpRequestException)
        {
            LogOutcome(stopwatch, "Failure", "Unavailable");
            throw new OllamaProviderException(OllamaFailureKind.Unavailable);
        }
        catch (JsonException)
        {
            LogOutcome(stopwatch, "Failure", "InvalidResponse");
            throw new OllamaProviderException(OllamaFailureKind.InvalidResponse);
        }
        catch (ResponseError)
        {
            LogOutcome(stopwatch, "Failure", "Unavailable");
            throw new OllamaProviderException(OllamaFailureKind.Unavailable);
        }
        catch (OllamaException)
        {
            LogOutcome(stopwatch, "Failure", "InvalidResponse");
            throw new OllamaProviderException(OllamaFailureKind.InvalidResponse);
        }
        catch (OllamaProviderException exception)
        {
            LogOutcome(stopwatch, "Failure", exception.FailureKind.ToString());
            throw;
        }
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text)
        {
            ModelId = response.ModelId
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceType == typeof(ChatClientMetadata) ? Metadata
            : serviceType.IsInstanceOfType(this) ? this
            : null;
    }

    public void Dispose() => transport.Dispose();

    private ChatOptions CreateRequestOptions(ChatOptions? requestOptions)
    {
        var boundedOptions = requestOptions?.Clone() ?? new ChatOptions();
        boundedOptions.ModelId = options.Model;
        boundedOptions.Temperature = (float)options.Temperature;
        boundedOptions.MaxOutputTokens = options.MaxOutputTokens;
        boundedOptions.Tools = null;
        boundedOptions.AddOllamaOption(OllamaOption.NumCtx, options.ContextLength);
        return boundedOptions;
    }

    private void LogOutcome(Stopwatch stopwatch, string outcome, string failureCategory)
    {
        logger.LogInformation(
            "Ollama operation completed. Provider: {Provider}; Model: {Model}; Operation: {Operation}; DurationMilliseconds: {DurationMilliseconds}; Outcome: {Outcome}; FailureCategory: {FailureCategory}",
            "Ollama",
            options.Model,
            "chat",
            stopwatch.ElapsedMilliseconds,
            outcome,
            failureCategory);
    }
}
