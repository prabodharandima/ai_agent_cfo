using System.Text.Json;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CfoAgent.Api.Mcp;

public sealed class KnowledgeFileMcpHttpClient(
    IOptions<McpOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<KnowledgeFileMcpHttpClient> logger) : IKnowledgeFileMcpRemoteClient, IAsyncDisposable
{
    public const string HttpClientName = "KnowledgeFileMcp";
    private const string DependencyName = "Knowledge File MCP";
    private static readonly string[] RequiredTools = ["list_knowledge_files", "read_knowledge_file"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private McpClient? client;

    public Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return ExecuteDependencyOperationAsync(async token =>
        {
            var connectedClient = await GetClientAsync(token);
            var names = (await connectedClient.ListToolsAsync(cancellationToken: token))
                .Select(tool => tool.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var missing = RequiredTools.Except(names, StringComparer.Ordinal).Any();
            var unexpected = names.Except(RequiredTools, StringComparer.Ordinal).Any();
            if (missing || unexpected)
            {
                throw new McpDependencyException(DependencyName, McpDependencyFailureKind.CapabilityMismatch);
            }

            logger.LogInformation("Knowledge File MCP capability discovery succeeded with {ToolCount} read-only tools.", names.Length);
            return (IReadOnlyList<string>)names;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken) =>
        await CallToolAsync<string[]>("list_knowledge_files", null, cancellationToken);

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
        return await ExecuteDependencyOperationAsync(async token =>
        {
            var connectedClient = await GetClientAsync(token);
            var result = await connectedClient.CallToolAsync(toolName, arguments, cancellationToken: token);
            if (result.IsError == true)
            {
                throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse);
            }

            var content = result.Content.OfType<TextContentBlock>().SingleOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse);
            }

            KnowledgeFileMcpResponse<T>? response;
            try
            {
                response = JsonSerializer.Deserialize<KnowledgeFileMcpResponse<T>>(content, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse, exception);
            }

            if (response is null || !response.IsSuccess || response.Data is null)
            {
                throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse);
            }

            logger.LogInformation("Knowledge File MCP tool {ToolName} completed successfully.", toolName);
            return response.Data;
        }, cancellationToken);
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

            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = FinanceMcpClient.CreateMcpEndpoint(options.Value.KnowledgeFiles.BaseUrl),
                    TransportMode = HttpTransportMode.StreamableHttp,
                    ConnectionTimeout = TimeSpan.FromSeconds(options.Value.KnowledgeFiles.TimeoutSeconds)
                },
                httpClient,
                ownsHttpClient: true);
            try
            {
                client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            }
            catch
            {
                await transport.DisposeAsync();
                throw;
            }

            logger.LogInformation("Connected to Knowledge File MCP over Streamable HTTP.");
            return client;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<T> ExecuteDependencyOperationAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.KnowledgeFiles.TimeoutSeconds));
        try
        {
            return await operation(timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new McpDependencyException(DependencyName, McpDependencyFailureKind.Timeout, exception);
        }
        catch (McpDependencyException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new McpDependencyException(DependencyName, McpDependencyFailureKind.Unavailable, exception);
        }
    }

    private void EnsureEnabled()
    {
        if (!options.Value.KnowledgeFiles.Enabled)
        {
            throw new McpDependencyException(DependencyName, McpDependencyFailureKind.Disabled);
        }
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
