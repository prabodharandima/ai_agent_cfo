using CfoAgent.KnowledgeFileMcpServer.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CfoAgent.KnowledgeFileMcpServer;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var root = GetRequiredRoot(args, builder.Configuration);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton(new KnowledgeRoot(root));
        builder.Services.AddHealthChecks()
            .AddCheck<KnowledgeRootReadinessHealthCheck>("knowledge-root", tags: ["ready"]);
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithToolsFromAssembly();

        await using var host = builder.Build();
        host.MapMcp("/mcp");
        host.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });
        host.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready", StringComparer.Ordinal)
        });

        await host.RunAsync();
    }

    private static string GetRequiredRoot(string[] args, IConfiguration configuration)
    {
        var rootIndex = Array.FindIndex(args, value => string.Equals(value, "--root", StringComparison.Ordinal));
        var configuredRoot = rootIndex >= 0 && rootIndex < args.Length - 1
            ? args[rootIndex + 1]
            : configuration[KnowledgeRoot.ConfigurationKey];
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new ArgumentException("The restricted knowledge root must be configured.");
        }

        var root = Path.GetFullPath(configuredRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException("The restricted knowledge root does not exist.");
        }

        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
        {
            throw new UnauthorizedAccessException("The restricted knowledge root cannot be a symbolic link or junction.");
        }

        return root;
    }
}

public sealed record KnowledgeRoot(string FullPath)
{
    public const string ConfigurationKey = "KnowledgeFiles:RootPath";

    public const string EnvironmentVariableName = "KnowledgeFiles__RootPath";
}
