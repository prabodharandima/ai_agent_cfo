using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Mcp;

public sealed class KnowledgeFileMcpClient(
    IOptions<McpOptions> options,
    IHostEnvironment environment,
    ILogger<KnowledgeFileMcpClient> logger) : IKnowledgeFileMcpClient
{
    public async Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(cancellationToken);
        var root = GetKnowledgeRoot();

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException("The configured knowledge directory does not exist.");
        }

        var files = new List<string>();
        var enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        foreach (var path in Directory.EnumerateFiles(root, "*", enumeration))
        {
            timeout.Token.ThrowIfCancellationRequested();
            EnsurePathIsUnderRoot(path, root);
            files.Add(Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'));
        }

        files.Sort(StringComparer.Ordinal);
        logger.LogInformation("Listed {FileCount} files from the restricted knowledge directory.", files.Count);
        await Task.CompletedTask;
        return files;
    }

    public async Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("A relative knowledge file path is required.", nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Absolute knowledge file paths are not permitted.", nameof(relativePath));
        }

        var segments = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            throw new ArgumentException("Knowledge file path traversal is not permitted.", nameof(relativePath));
        }

        using var timeout = CreateTimeout(cancellationToken);
        var root = GetKnowledgeRoot();
        var resolvedPath = Path.GetFullPath(Path.Combine(root, relativePath));
        EnsurePathIsUnderRoot(resolvedPath, root);

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("The requested knowledge file does not exist.", relativePath);
        }

        EnsureNoReparsePoints(resolvedPath, root);

        var content = await File.ReadAllTextAsync(resolvedPath, timeout.Token);
        logger.LogInformation("Read knowledge file {RelativePath}.", Path.GetRelativePath(root, resolvedPath));
        return content;
    }

    private string GetKnowledgeRoot()
    {
        var root = KnowledgeFilePathResolver.ResolveRoot(options.Value, environment);
        if (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
        {
            throw new UnauthorizedAccessException("The knowledge directory cannot be a symbolic link or junction.");
        }

        return root;
    }

    private static void EnsureNoReparsePoints(string path, string root)
    {
        var current = root;
        foreach (var segment in Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException("Symbolic links and junctions are not permitted in knowledge file paths.");
            }
        }
    }

    private static void EnsurePathIsUnderRoot(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, comparison))
        {
            throw new UnauthorizedAccessException("Knowledge file access is restricted to the configured knowledge directory.");
        }
    }

    private CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        var timeoutSeconds = options.Value.KnowledgeFiles.TimeoutSeconds;
        if (timeoutSeconds <= 0)
        {
            throw new InvalidOperationException("Mcp:TimeoutSeconds must be greater than zero.");
        }

        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return source;
    }
}
