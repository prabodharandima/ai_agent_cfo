using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Mcp;

public sealed class KnowledgeFileMcpFallback(IOptions<McpOptions> options, ILogger<KnowledgeFileMcpFallback> logger)
{
    public Task<McpFallbackResult<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> mcpOperation,
        Func<CancellationToken, Task<T>> directOperation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mcpOperation);
        ArgumentNullException.ThrowIfNull(directOperation);

        return !options.Value.KnowledgeFiles.Enabled
            ? UseDirectAsync(directOperation, "disabled", null, cancellationToken)
            : TryMcpAsync(mcpOperation, directOperation, cancellationToken);
    }

    private async Task<McpFallbackResult<T>> TryMcpAsync<T>(
        Func<CancellationToken, Task<T>> mcpOperation,
        Func<CancellationToken, Task<T>> directOperation,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await mcpOperation(cancellationToken);
            return new McpFallbackResult<T>(value, false, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (options.Value.UseLocalFallback)
        {
            return await UseDirectAsync(directOperation, exception is OperationCanceledException or TimeoutException ? "timeout" : "unavailable", exception, cancellationToken);
        }
    }

    private async Task<McpFallbackResult<T>> UseDirectAsync<T>(
        Func<CancellationToken, Task<T>> directOperation,
        string reason,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        if (exception is null)
        {
            logger.LogInformation("Knowledge file MCP fallback used because the integration is disabled.");
        }
        else
        {
            logger.LogWarning(
                "Knowledge file MCP fallback used because the integration was {FallbackReason}. Failure type: {FailureType}.",
                reason,
                exception.GetType().Name);
        }

        var value = await directOperation(cancellationToken);
        return new McpFallbackResult<T>(value, true, reason);
    }
}
