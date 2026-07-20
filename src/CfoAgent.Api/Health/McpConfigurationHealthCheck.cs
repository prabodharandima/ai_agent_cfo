using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Health;

public sealed class McpConfigurationHealthCheck(
    IOptions<McpOptions> options,
    IFinanceMcpRemoteClient financeClient,
    IKnowledgeFileMcpRemoteClient knowledgeRemoteClient,
    IKnowledgeFileMcpClient knowledgeClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var problems = new List<string>();

        if (!options.Value.Finance.Enabled)
        {
            problems.Add("Finance MCP is disabled.");
        }
        else
        {
            try
            {
                await financeClient.DiscoverToolsAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (McpDependencyException)
            {
                problems.Add("Finance MCP is unavailable.");
            }
        }

        var knowledge = options.Value.KnowledgeFiles;
        if (knowledge.Enabled)
        {
            try
            {
                await knowledgeRemoteClient.DiscoverToolsAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (McpDependencyException)
            {
                problems.Add("Knowledge File MCP is unavailable.");
            }
        }
        else if (knowledge.UseLocalFallback)
        {
            try
            {
                await knowledgeClient.ListFilesAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is McpDependencyException or IOException or InvalidOperationException)
            {
                problems.Add("Knowledge File MCP is unavailable.");
            }
        }

        return problems.Count == 0
            ? HealthCheckResult.Healthy("Configured MCP dependencies are ready.")
            : HealthCheckResult.Unhealthy(string.Join(' ', problems));
    }
}
