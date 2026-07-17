using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Health;

public sealed class McpConfigurationHealthCheck(IOptions<McpOptions> options, IHostEnvironment environment) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        var problems = new List<string>();

        if (configuration.Finance.Enabled)
        {
            var projectPath = Path.GetFullPath(configuration.Finance.ServerProjectPath, environment.ContentRootPath);
            if (!File.Exists(Path.Combine(projectPath, "CfoAgent.FinanceMcpServer.csproj")))
            {
                problems.Add("Finance MCP project is unavailable.");
            }
        }

        if (configuration.KnowledgeFiles.Enabled)
        {
            try
            {
                var knowledgeRoot = KnowledgeFilePathResolver.ResolveRoot(configuration, environment);
                var serverProject = KnowledgeFilePathResolver.ResolveServerProject(environment);
                if (!Directory.Exists(knowledgeRoot) || !Directory.Exists(serverProject))
                {
                    problems.Add("Knowledge file MCP configuration is unavailable.");
                }
            }
            catch (Exception exception) when (exception is DirectoryNotFoundException or FileNotFoundException or InvalidOperationException)
            {
                problems.Add("Knowledge file MCP configuration is unavailable.");
            }
        }

        return Task.FromResult(problems.Count == 0
            ? HealthCheckResult.Healthy("Configured MCP integrations are ready for lazy startup.")
            : HealthCheckResult.Unhealthy(string.Join(' ', problems)));
    }
}
