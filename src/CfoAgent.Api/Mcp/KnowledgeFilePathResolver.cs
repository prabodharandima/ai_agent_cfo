using CfoAgent.Api.Configuration;

namespace CfoAgent.Api.Mcp;

internal static class KnowledgeFilePathResolver
{
    public static string ResolveRoot(McpOptions options, IHostEnvironment environment)
    {
        var configuredRoot = options.KnowledgeFiles.RootPath;
        if (string.IsNullOrWhiteSpace(configuredRoot))
        {
            throw new InvalidOperationException("Mcp:KnowledgeFiles:RootPath is required.");
        }

        if (Path.IsPathRooted(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        return FindExistingDirectory(configuredRoot, environment)
            ?? Path.GetFullPath(configuredRoot, environment.ContentRootPath);
    }

    public static string ResolveServerProject(IHostEnvironment environment)
    {
        const string relativeProject = "tools/CfoAgent.KnowledgeFileMcpServer";
        return FindExistingDirectory(relativeProject, environment)
            ?? throw new FileNotFoundException("The restricted knowledge filesystem MCP server project was not found.");
    }

    private static string? FindExistingDirectory(string relativePath, IHostEnvironment environment)
    {
        foreach (var startPath in new[] { environment.ContentRootPath, AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(Path.GetFullPath(startPath)); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.GetFullPath(relativePath, directory.FullName);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
