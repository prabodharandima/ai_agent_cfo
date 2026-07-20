using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Mcp;

public sealed class KnowledgeFileMcpAccess(
    IKnowledgeFileMcpRemoteClient remoteClient,
    KnowledgeFileMcpClient localClient,
    IOptions<McpOptions> options,
    IHostEnvironment environment,
    ILogger<KnowledgeFileMcpAccess> logger) : IKnowledgeFileMcpClient
{
    public async Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            remoteClient.ListFilesAsync,
            localClient.ListFilesAsync,
            cancellationToken);
    }

    public async Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        KnowledgeFileMcpHttpClient.ValidateRelativePath(relativePath);
        return await ExecuteAsync(
            token => remoteClient.ReadFileAsync(relativePath, token),
            token => localClient.ReadFileAsync(relativePath, token),
            cancellationToken);
    }

    private async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> remoteOperation,
        Func<CancellationToken, Task<T>> localOperation,
        CancellationToken cancellationToken)
    {
        if (!options.Value.KnowledgeFiles.Enabled)
        {
            if (!CanUseLocalFallback)
            {
                throw new McpDependencyException("Knowledge File MCP", McpDependencyFailureKind.Disabled);
            }

            logger.LogInformation("Knowledge file MCP fallback used because the integration is disabled.");
            return await localOperation(cancellationToken);
        }

        try
        {
            return await remoteOperation(cancellationToken);
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

            logger.LogWarning(
                "Knowledge file MCP fallback used because the integration was {FallbackReason}. Failure type: {FailureType}.",
                reason,
                exception.GetType().Name);
            return await localOperation(cancellationToken);
        }
    }

    private bool CanUseLocalFallback =>
        options.Value.KnowledgeFiles.UseLocalFallback && environment.IsDevelopment();
}
