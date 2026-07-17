using System.Text.Json;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CfoAgent.Api.Mcp;

public sealed class KnowledgeFileMcpProcessClient(
    IOptions<McpOptions> options,
    IHostEnvironment environment,
    ILogger<KnowledgeFileMcpProcessClient> logger) : IKnowledgeFileMcpProcessClient, IAsyncDisposable
{
    private static readonly string[] RequiredTools = ["list_knowledge_files", "read_knowledge_file"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private McpClient? client;

    public async Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var connectedClient = await GetClientAsync(cancellationToken);
        using var timeout = CreateTimeout(cancellationToken);
        var tools = await connectedClient.ListToolsAsync(cancellationToken: timeout.Token);
        var names = tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var missing = RequiredTools.Except(names, StringComparer.Ordinal).ToArray();
        var unexpected = names.Except(RequiredTools, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0 || unexpected.Length > 0)
        {
            throw new InvalidOperationException("The restricted knowledge filesystem MCP server does not expose the required read-only capabilities.");
        }

        logger.LogInformation("Knowledge filesystem MCP capability discovery succeeded with {ToolCount} read-only tools.", names.Length);
        return names;
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken)
    {
        var response = await CallToolAsync<string[]>("list_knowledge_files", null, cancellationToken);
        return response;
    }

    public Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        ValidateRelativePath(relativePath);
        return CallToolAsync<string>(
            "read_knowledge_file",
            new Dictionary<string, object?> { ["relativePath"] = relativePath },
            cancellationToken);
    }

    private async Task<T> CallToolAsync<T>(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken)
    {
        await DiscoverToolsAsync(cancellationToken);
        var connectedClient = await GetClientAsync(cancellationToken);
        using var timeout = CreateTimeout(cancellationToken);
        var result = await connectedClient.CallToolAsync(toolName, arguments, cancellationToken: timeout.Token);
        if (result.IsError == true)
        {
            throw new InvalidOperationException($"Knowledge filesystem MCP tool '{toolName}' reported an error.");
        }

        var content = result.Content.OfType<TextContentBlock>().SingleOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Knowledge filesystem MCP tool '{toolName}' returned no structured result.");
        }

        var response = JsonSerializer.Deserialize<KnowledgeFileMcpResponse<T>>(content, JsonOptions)
            ?? throw new InvalidOperationException($"Knowledge filesystem MCP tool '{toolName}' returned an invalid result.");
        if (!response.IsSuccess || response.Data is null)
        {
            throw new InvalidOperationException($"Knowledge filesystem MCP tool '{toolName}' could not complete the request.");
        }

        logger.LogInformation("Knowledge filesystem MCP tool {ToolName} completed successfully.", toolName);
        return response.Data;
    }

    private async Task<McpClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (client is not null)
        {
            return client;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (client is not null)
            {
                return client;
            }

            EnsureEnabled();
            var root = KnowledgeFilePathResolver.ResolveRoot(options.Value, environment);
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException("The configured knowledge directory does not exist.");
            }

            var projectPath = KnowledgeFilePathResolver.ResolveServerProject(environment);
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = "dotnet",
                Arguments = ["run", "--project", projectPath, "--no-build", "--configuration", GetBuildConfiguration(), "--", "--root", root]
            });
            using var timeout = CreateTimeout(cancellationToken);
            client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
            logger.LogInformation("Connected to the restricted knowledge filesystem MCP server.");
            return client;
        }
        finally
        {
            gate.Release();
        }
    }

    private void EnsureEnabled()
    {
        if (!options.Value.KnowledgeFiles.Enabled)
        {
            throw new InvalidOperationException("Knowledge filesystem MCP is disabled by configuration.");
        }
    }

    private CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(options.Value.KnowledgeFiles.TimeoutSeconds));
        return source;
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

    private static string GetBuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (client is not null)
            {
                await client.DisposeAsync();
            }

            client = null;
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }

    private sealed record KnowledgeFileMcpResponse<T>(bool IsSuccess, T? Data, string? Error);
}
