using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CfoAgent.KnowledgeFileMcpServer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var root = GetRequiredRoot(args);
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Services.AddSingleton(new KnowledgeRoot(root));
        builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }

    private static string GetRequiredRoot(string[] args)
    {
        var rootIndex = Array.FindIndex(args, value => string.Equals(value, "--root", StringComparison.Ordinal));
        if (rootIndex < 0 || rootIndex == args.Length - 1 || string.IsNullOrWhiteSpace(args[rootIndex + 1]))
        {
            throw new ArgumentException("The restricted knowledge root must be provided with --root.");
        }

        var root = Path.GetFullPath(args[rootIndex + 1]);
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

public sealed record KnowledgeRoot(string FullPath);
