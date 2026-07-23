using System.Diagnostics;
using System.Text.RegularExpressions;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.AI;

public sealed partial class AgentChatMiddleware(
    IOptions<AgentMiddlewareOptions> options,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AgentChatMiddleware> logger)
{
    private readonly AgentMiddlewareOptions _options = options.Value;

    public IChatClient Wrap(IChatClient client) => client.AsBuilder()
        .Use((messages, chatOptions, innerClient, cancellationToken) =>
            GetResponseAsync(messages, chatOptions, innerClient, cancellationToken),
            GetStreamingResponseAsync)
        .Build();

    private async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions,
        IChatClient innerClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var requestMessages = messages.ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var correlationId = httpContextAccessor.HttpContext?.TraceIdentifier
            ?? Activity.Current?.TraceId.ToString()
            ?? "none";
        var metadata = GetMetadata(innerClient, chatOptions);

        if (ContainsSuspiciousPrompt(requestMessages))
        {
            logger.LogWarning(
                "Agent chat request blocked. CorrelationId: {CorrelationId}; Provider: {Provider}; Model: {Model}; MessageCount: {MessageCount}; Outcome: {Outcome}",
                correlationId,
                metadata.ProviderName,
                metadata.DefaultModelId,
                requestMessages.Length,
                "Blocked");
            throw new PromptInjectionRiskException();
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await innerClient.GetResponseAsync(requestMessages, chatOptions, cancellationToken);
            var redactedResponse = RedactSensitiveOutput(response, out var wasRedacted);
            LogOutcome(correlationId, metadata, requestMessages.Length, stopwatch, "Success", wasRedacted);
            return redactedResponse;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogOutcome(correlationId, metadata, requestMessages.Length, stopwatch, "Cancelled", wasRedacted: false);
            throw;
        }
        catch
        {
            LogOutcome(correlationId, metadata, requestMessages.Length, stopwatch, "Failure", wasRedacted: false);
            throw;
        }
    }

    private async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions,
        IChatClient innerClient,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var requestMessages = messages.ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var correlationId = httpContextAccessor.HttpContext?.TraceIdentifier
            ?? Activity.Current?.TraceId.ToString()
            ?? "none";
        var metadata = GetMetadata(innerClient, chatOptions);

        if (ContainsSuspiciousPrompt(requestMessages))
        {
            logger.LogWarning(
                "Agent chat request blocked. CorrelationId: {CorrelationId}; Provider: {Provider}; Model: {Model}; MessageCount: {MessageCount}; Outcome: {Outcome}",
                correlationId,
                metadata.ProviderName,
                metadata.DefaultModelId,
                requestMessages.Length,
                "Blocked");
            throw new PromptInjectionRiskException();
        }

        var stopwatch = Stopwatch.StartNew();
        var outputRedacted = false;
        await using var enumerator = innerClient
            .GetStreamingResponseAsync(requestMessages, chatOptions, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

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
                LogOutcome(correlationId, metadata, requestMessages.Length, stopwatch, "Cancelled", wasRedacted: outputRedacted);
                throw;
            }
            catch
            {
                LogOutcome(correlationId, metadata, requestMessages.Length, stopwatch, "Failure", wasRedacted: outputRedacted);
                throw;
            }

            yield return RedactStreamingUpdate(update, ref outputRedacted);
        }

        LogOutcome(correlationId, metadata, requestMessages.Length, stopwatch, "Success", outputRedacted);
    }

    private bool ContainsSuspiciousPrompt(IReadOnlyList<ChatMessage> messages) =>
        _options.PromptInjectionCheckEnabled
        && messages.Any(message => _options.SuspiciousPromptPhrases.Any(phrase =>
            message.Text?.Contains(phrase, StringComparison.OrdinalIgnoreCase) == true));

    private static ChatResponse RedactSensitiveOutput(ChatResponse response, out bool wasRedacted)
    {
        ArgumentNullException.ThrowIfNull(response);

        wasRedacted = false;
        var messages = new ChatMessage[response.Messages.Count];
        for (var index = 0; index < response.Messages.Count; index++)
        {
            messages[index] = RedactMessage(response.Messages[index], ref wasRedacted);
        }

        if (!wasRedacted)
        {
            return response;
        }

        return new ChatResponse(messages)
        {
            ResponseId = response.ResponseId,
            ConversationId = response.ConversationId,
            ModelId = response.ModelId,
            CreatedAt = response.CreatedAt,
            FinishReason = response.FinishReason,
            Usage = response.Usage,
            ContinuationToken = response.ContinuationToken
        };
    }

    private static ChatMessage RedactMessage(ChatMessage message, ref bool wasRedacted)
    {
        var contents = new AIContent[message.Contents.Count];
        for (var index = 0; index < message.Contents.Count; index++)
        {
            contents[index] = message.Contents[index] is TextContent text
                ? RedactTextContent(text, ref wasRedacted)
                : message.Contents[index];
        }

        return new ChatMessage(message.Role, contents)
        {
            AuthorName = message.AuthorName
        };
    }

    private static AIContent RedactTextContent(TextContent content, ref bool wasRedacted)
    {
        var redacted = Redact(content.Text);
        if (string.Equals(redacted, content.Text, StringComparison.Ordinal))
        {
            return content;
        }

        wasRedacted = true;
        return new TextContent(redacted);
    }

    private static ChatResponseUpdate RedactStreamingUpdate(ChatResponseUpdate update, ref bool wasRedacted)
    {
        if (string.IsNullOrWhiteSpace(update.Text))
        {
            return update;
        }

        var redacted = Redact(update.Text);
        if (string.Equals(redacted, update.Text, StringComparison.Ordinal))
        {
            return update;
        }

        wasRedacted = true;
        return new ChatResponseUpdate(update.Role, redacted)
        {
            ModelId = update.ModelId
        };
    }

    private static string Redact(string value)
    {
        var result = SensitiveAssignment().Replace(value, "$1=[REDACTED]");
        result = BearerCredential().Replace(result, "Bearer [REDACTED]");
        return ConnectionString().Replace(result, "[REDACTED_CONNECTION_STRING]");
    }

    private void LogOutcome(
        string correlationId,
        ChatClientMetadata metadata,
        int messageCount,
        Stopwatch stopwatch,
        string outcome,
        bool wasRedacted)
    {
        logger.LogInformation(
            "Agent chat operation completed. CorrelationId: {CorrelationId}; Provider: {Provider}; Model: {Model}; MessageCount: {MessageCount}; DurationMilliseconds: {DurationMilliseconds}; Outcome: {Outcome}; OutputRedacted: {OutputRedacted}",
            correlationId,
            metadata.ProviderName,
            metadata.DefaultModelId,
            messageCount,
            stopwatch.ElapsedMilliseconds,
            outcome,
            wasRedacted);
    }

    private static ChatClientMetadata GetMetadata(IChatClient client, ChatOptions? options) =>
        client.GetService(typeof(ChatClientMetadata)) as ChatClientMetadata
        ?? new ChatClientMetadata("Unknown", new Uri("https://unknown.local"), options?.ModelId ?? "unknown");

    [GeneratedRegex(@"(?i)\b(api[_-]?key|password|secret|token)\b\s*[:=]\s*(?!\[REDACTED\])\S+", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveAssignment();

    [GeneratedRegex(@"(?i)\bbearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex BearerCredential();

    [GeneratedRegex(@"(?i)\bpostgres(?:ql)?://\S+", RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionString();
}
