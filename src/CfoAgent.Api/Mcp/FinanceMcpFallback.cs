using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Mcp;

public sealed class FinanceMcpFallback(IOptions<McpOptions> options, ILogger<FinanceMcpFallback> logger)
{
    public Task<McpFallbackResult<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> mcpOperation,
        Func<CancellationToken, Task<T>> localOperation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mcpOperation);
        ArgumentNullException.ThrowIfNull(localOperation);

        return !options.Value.Finance.Enabled
            ? UseLocalAsync(localOperation, "disabled", null, cancellationToken)
            : TryMcpAsync(mcpOperation, localOperation, cancellationToken);
    }

    private async Task<McpFallbackResult<T>> TryMcpAsync<T>(
        Func<CancellationToken, Task<T>> mcpOperation,
        Func<CancellationToken, Task<T>> localOperation,
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
            return await UseLocalAsync(localOperation, exception is OperationCanceledException or TimeoutException ? "timeout" : "unavailable", exception, cancellationToken);
        }
    }

    private async Task<McpFallbackResult<T>> UseLocalAsync<T>(
        Func<CancellationToken, Task<T>> localOperation,
        string reason,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        if (exception is null)
        {
            logger.LogInformation("Finance MCP fallback used because the integration is disabled.");
        }
        else
        {
            logger.LogWarning(
                "Finance MCP fallback used because the integration was {FallbackReason}. Failure type: {FailureType}.",
                reason,
                exception.GetType().Name);
        }

        var value = await localOperation(cancellationToken);
        return new McpFallbackResult<T>(value, true, reason);
    }
}
