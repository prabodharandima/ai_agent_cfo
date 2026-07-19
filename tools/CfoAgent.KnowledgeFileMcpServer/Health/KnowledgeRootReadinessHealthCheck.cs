using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CfoAgent.KnowledgeFileMcpServer.Health;

public sealed class KnowledgeRootReadinessHealthCheck(
    KnowledgeRoot root,
    ILogger<KnowledgeRootReadinessHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root.FullPath))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("The knowledge root is unavailable."));
            }

            if ((File.GetAttributes(root.FullPath) & FileAttributes.ReparsePoint) != 0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("The knowledge root is not permitted."));
            }

            _ = Directory.EnumerateFileSystemEntries(root.FullPath).Take(1).ToArray();
            return Task.FromResult(HealthCheckResult.Healthy("The knowledge root is ready."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Knowledge root readiness failed with {ExceptionType}.",
                exception.GetType().Name);
            return Task.FromResult(HealthCheckResult.Unhealthy("The knowledge root is unavailable."));
        }
    }
}
