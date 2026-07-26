using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CfoAgent.Api.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Exceptions;

namespace CfoAgent.Api.AI.Ollama;

public sealed class OllamaChatClient : IChatClient
{
    private readonly IChatClient transport;
    private readonly OllamaOptions options;
    private readonly AiProviderDescriptor provider;
    private readonly ILogger<OllamaChatClient> logger;

    // OKF -
    public OllamaChatClient(
        IChatClient transport,
        OllamaOptions options,
        AiProviderDescriptor provider,
        ILogger<OllamaChatClient>? logger = null)
    {
        this.transport = transport;
        this.options = options;
        this.provider = provider;
        this.logger = logger ?? NullLogger<OllamaChatClient>.Instance;
        Metadata = new ChatClientMetadata(provider.ProviderName, new Uri(options.BaseUrl), provider.ModelName);
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
            var hasFunctionCall = response.Messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionCallContent>()
                .Any();
            if (string.IsNullOrWhiteSpace(response.Text) && !hasFunctionCall)
            {
                throw new AiProviderException(provider.ProviderName, AiProviderFailureKind.InvalidResponse);
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
            throw new AiProviderException(provider.ProviderName, AiProviderFailureKind.Timeout);
        }
        catch (HttpRequestException)
        {
            LogOutcome(stopwatch, "Failure", "Unavailable");
            throw new AiProviderException(provider.ProviderName, AiProviderFailureKind.Unavailable);
        }
        catch (JsonException)
        {
            LogOutcome(stopwatch, "Failure", "InvalidResponse");
            throw new AiProviderException(provider.ProviderName, AiProviderFailureKind.InvalidResponse);
        }
        catch (ResponseError)
        {
            LogOutcome(stopwatch, "Failure", "Unavailable");
            throw new AiProviderException(provider.ProviderName, AiProviderFailureKind.Unavailable);
        }
        catch (OllamaException)
        {
            LogOutcome(stopwatch, "Failure", "InvalidResponse");
            throw new AiProviderException(provider.ProviderName, AiProviderFailureKind.InvalidResponse);
        }
        catch (AiProviderException exception)
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
        ArgumentNullException.ThrowIfNull(messages);

        var requestOptions = CreateRequestOptions(options);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(this.options.TimeoutSeconds));
        var stopwatch = Stopwatch.StartNew();
        await using var enumerator = transport
            .GetStreamingResponseAsync(messages, requestOptions, timeoutSource.Token)
            .GetAsyncEnumerator(timeoutSource.Token);

        while (true)
        {
            ChatResponseUpdate update;
            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    break;
                }

                update = enumerator.Current;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                LogOutcome(stopwatch, "Cancelled", "CallerCancelled");
                throw;
            }
            catch (OperationCanceledException)
            {
                LogOutcome(stopwatch, "Failure", "Timeout");
                throw new AiProviderException(provider.ProviderName, AiProviderFailureKind.Timeout);
            }
            catch (HttpRequestException)
            {
                LogOutcome(stopwatch, "Failure", "Unavailable");
                throw new AiProviderException(provider.ProviderName, AiProviderFailureKind.Unavailable);
            }
            catch (JsonException)
            {
                LogOutcome(stopwatch, "Failure", "InvalidResponse");
                throw new AiProviderException(provider.ProviderName, AiProviderFailureKind.InvalidResponse);
            }
            catch (ResponseError)
            {
                LogOutcome(stopwatch, "Failure", "Unavailable");
                throw new AiProviderException(provider.ProviderName, AiProviderFailureKind.Unavailable);
            }
            catch (OllamaException)
            {
                LogOutcome(stopwatch, "Failure", "InvalidResponse");
                throw new AiProviderException(provider.ProviderName, AiProviderFailureKind.InvalidResponse);
            }

            yield return update;
        }

        LogOutcome(stopwatch, "Success", "None");
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
        boundedOptions.ModelId = provider.ModelName;
        boundedOptions.Temperature = (float)options.Temperature;
        boundedOptions.MaxOutputTokens = options.MaxOutputTokens;
        boundedOptions.AddOllamaOption(OllamaOption.NumCtx, options.ContextLength);
        return boundedOptions;
    }

    private void LogOutcome(Stopwatch stopwatch, string outcome, string failureCategory)
    {
        logger.LogInformation(
            "AI provider operation completed. Provider: {Provider}; Model: {Model}; Operation: {Operation}; DurationMilliseconds: {DurationMilliseconds}; Outcome: {Outcome}; FailureCategory: {FailureCategory}",
            provider.ProviderName,
            provider.ModelName,
            "chat",
            stopwatch.ElapsedMilliseconds,
            outcome,
            failureCategory);
    }
}
