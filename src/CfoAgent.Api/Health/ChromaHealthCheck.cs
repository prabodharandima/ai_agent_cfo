using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CfoAgent.Api.Health;

public sealed class ChromaHealthCheck(HttpClient httpClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("api/v2/heartbeat", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("ChromaDB is reachable.")
                : HealthCheckResult.Unhealthy("ChromaDB returned an unsuccessful status.");
        }
        catch (HttpRequestException)
        {
            return HealthCheckResult.Unhealthy("ChromaDB is unavailable.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("ChromaDB health check timed out.");
        }
    }
}
