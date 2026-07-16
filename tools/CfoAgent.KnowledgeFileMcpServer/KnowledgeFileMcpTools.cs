using System.ComponentModel;
using ModelContextProtocol.Server;

namespace CfoAgent.KnowledgeFileMcpServer;

[McpServerToolType]
public sealed class KnowledgeFileMcpTools(KnowledgeRoot root)
{
    [McpServerTool(Name = "list_knowledge_files")]
    [Description("Lists read-only files below the configured knowledge root.")]
    public Task<KnowledgeFileMcpResult<IReadOnlyList<string>>> ListFilesAsync(CancellationToken cancellationToken)
    {
        var files = new List<string>();
        var enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(root.FullPath, "*", enumeration))
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsurePathIsUnderRoot(path);
            files.Add(Path.GetRelativePath(root.FullPath, path).Replace(Path.DirectorySeparatorChar, '/'));
        }

        files.Sort(StringComparer.Ordinal);
        return Task.FromResult(KnowledgeFileMcpResult<IReadOnlyList<string>>.Success(files));
    }

    [McpServerTool(Name = "read_knowledge_file")]
    [Description("Reads one file below the configured knowledge root without permitting filesystem changes.")]
    public async Task<KnowledgeFileMcpResult<string>> ReadFileAsync(
        [Description("Relative file path returned by list_knowledge_files.")] string relativePath,
        CancellationToken cancellationToken)
    {
        var path = ResolveAllowedPath(relativePath);
        if (!File.Exists(path))
        {
            return KnowledgeFileMcpResult<string>.Failure("The requested knowledge file does not exist.");
        }

        EnsureNoReparsePoints(path);
        return KnowledgeFileMcpResult<string>.Success(await File.ReadAllTextAsync(path, cancellationToken));
    }

    private string ResolveAllowedPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("A relative knowledge file path is required.", nameof(relativePath));
        }

        if (Path.IsPathRooted(relativePath))
        {
            throw new UnauthorizedAccessException("Absolute knowledge file paths are not permitted.");
        }

        var segments = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            throw new UnauthorizedAccessException("Knowledge file path traversal is not permitted.");
        }

        var path = Path.GetFullPath(Path.Combine(root.FullPath, relativePath));
        EnsurePathIsUnderRoot(path);
        return path;
    }

    private void EnsurePathIsUnderRoot(string path)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var rootPrefix = Path.TrimEndingDirectorySeparator(root.FullPath) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, comparison))
        {
            throw new UnauthorizedAccessException("Knowledge file access is restricted to the configured knowledge directory.");
        }
    }

    private void EnsureNoReparsePoints(string path)
    {
        var relativePath = Path.GetRelativePath(root.FullPath, path);
        var current = root.FullPath;
        foreach (var segment in relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current) || Directory.Exists(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new UnauthorizedAccessException("Symbolic links and junctions are not permitted in knowledge file paths.");
                }
            }
        }
    }
}

public sealed record KnowledgeFileMcpResult<T>(bool IsSuccess, T? Data, string? Error)
{
    public static KnowledgeFileMcpResult<T> Success(T data) => new(true, data, null);

    public static KnowledgeFileMcpResult<T> Failure(string error) => new(false, default, error);
}
