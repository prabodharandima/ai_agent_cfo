using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace CfoAgent.Api.Mcp;

public sealed class KnowledgeFileMcpHttpClient(
    [FromKeyedServices(McpToolAdapter.KnowledgeFilesKey)] IMcpToolAdapter toolAdapter) : IKnowledgeFileMcpRemoteClient
{
    private const string DependencyName = "Knowledge File MCP";
    private static readonly string[] RequiredTools = ["list_knowledge_files", "read_knowledge_file"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken)
    {
        var tools = await toolAdapter.GetApprovedToolNamesAsync(RequiredTools, cancellationToken);
        return tools.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken)
    {
        var data = await toolAdapter.CallApprovedToolAsync("list_knowledge_files", null, cancellationToken);
        return Deserialize<string[]>(data);
    }

    public async Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        ValidateRelativePath(relativePath);
        var data = await toolAdapter.CallApprovedToolAsync(
            "read_knowledge_file",
            new Dictionary<string, object?> { ["relativePath"] = relativePath },
            cancellationToken);
        return Deserialize<string>(data);
    }

    internal static void ValidateRelativePath(string relativePath)
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
    }

    private static T Deserialize<T>(JsonElement data)
    {
        try
        {
            return data.Deserialize<T>(JsonOptions)
                ?? throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse);
        }
        catch (JsonException exception)
        {
            throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse, exception);
        }
    }
}
