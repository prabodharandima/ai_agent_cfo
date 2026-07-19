using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Mcp;

public sealed class KnowledgeFileMcpFallback(
    IOptions<McpOptions> options,
    IHostEnvironment environment,
    ILogger<KnowledgeFileMcpFallback> logger)
{
    public Task<McpFallbackResult<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> mcpOperation,
        Func<CancellationToken, Task<T>> directOperation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mcpOperation);
        ArgumentNullException.ThrowIfNull(directOperation);

        return !options.Value.KnowledgeFiles.Enabled
            ? CanUseLocalFallback
                ? UseDirectAsync(directOperation, "disabled", null, cancellationToken)
                : Task.FromException<McpFallbackResult<T>>(new McpDependencyException("Knowledge File MCP", McpDependencyFailureKind.Disabled))
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
        catch (McpDependencyException exception) when (CanUseLocalFallback)
        {
            var reason = exception.FailureKind switch
            {
                McpDependencyFailureKind.Timeout => "timeout",
                McpDependencyFailureKind.CapabilityMismatch => "capability-mismatch",
                _ => "unavailable"
            };
            return await UseDirectAsync(directOperation, reason, exception, cancellationToken);
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

    private bool CanUseLocalFallback =>
        options.Value.KnowledgeFiles.UseLocalFallback && environment.IsDevelopment();
}
