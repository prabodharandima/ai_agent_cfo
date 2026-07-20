using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CfoAgent.Api.Mcp;

public sealed class McpToolAdapter : IMcpToolAdapter, IAsyncDisposable
{
    public const string FinanceKey = "Finance";
    public const string KnowledgeFilesKey = "KnowledgeFiles";
    public const string FinanceHttpClientName = "FinanceMcp";
    public const string KnowledgeFilesHttpClientName = "KnowledgeFileMcp";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string dependencyName;
    private readonly string httpClientName;
    private readonly bool enabled;
    private readonly string baseUrl;
    private readonly TimeSpan timeout;
    private readonly HashSet<string> allowedToolNames;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<McpToolAdapter> logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    private McpClient? client;
    private IReadOnlyDictionary<string, McpClientTool>? approvedTools;
    private bool disposed;

    public McpToolAdapter(
        string dependencyName,
        string httpClientName,
        bool enabled,
        string baseUrl,
        int timeoutSeconds,
        IEnumerable<string> allowedToolNames,
        IHttpClientFactory httpClientFactory,
        ILogger<McpToolAdapter> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpClientName);
        ArgumentNullException.ThrowIfNull(allowedToolNames);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        this.allowedToolNames = allowedToolNames.ToHashSet(StringComparer.Ordinal);
        if (this.allowedToolNames.Count == 0 || this.allowedToolNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one nonblank approved MCP tool name is required.", nameof(allowedToolNames));
        }

        if (timeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        }

        this.dependencyName = dependencyName;
        this.httpClientName = httpClientName;
        this.enabled = enabled;
        this.baseUrl = baseUrl;
        timeout = TimeSpan.FromSeconds(timeoutSeconds);
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    public Task<IReadOnlyList<string>> GetApprovedToolNamesAsync(
        IEnumerable<string>? requiredToolNames,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return ExecuteDependencyOperationAsync(async token =>
        {
            var tools = await GetOrDiscoverToolsAsync(token);
            if (requiredToolNames is null)
            {
                return (IReadOnlyList<string>)tools.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            }

            var requestedNames = requiredToolNames.ToHashSet(StringComparer.Ordinal);
            if (requestedNames.Count == 0 || requestedNames.Any(name => !allowedToolNames.Contains(name) || !tools.ContainsKey(name)))
            {
                throw new McpDependencyException(dependencyName, McpDependencyFailureKind.CapabilityMismatch);
            }

            return (IReadOnlyList<string>)requestedNames
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }, cancellationToken);
    }

    public Task<JsonElement> CallApprovedToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        EnsureEnabled();

        return ExecuteDependencyOperationAsync(async token =>
        {
            var tools = await GetOrDiscoverToolsAsync(token);
            if (!allowedToolNames.Contains(toolName) || !tools.TryGetValue(toolName, out var tool))
            {
                throw new McpDependencyException(dependencyName, McpDependencyFailureKind.CapabilityMismatch);
            }

            var result = await tool.CallAsync(arguments, cancellationToken: token);
            if (result.IsError == true)
            {
                await ResetConnectionAsync();
                throw new McpDependencyException(dependencyName, McpDependencyFailureKind.InvalidResponse);
            }

            var content = result.Content.OfType<TextContentBlock>().SingleOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new McpDependencyException(dependencyName, McpDependencyFailureKind.InvalidResponse);
            }

            McpResponseEnvelope? response;
            try
            {
                response = JsonSerializer.Deserialize<McpResponseEnvelope>(content, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new McpDependencyException(dependencyName, McpDependencyFailureKind.InvalidResponse, exception);
            }

            if (response is null || !response.IsSuccess || response.Data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                throw new McpDependencyException(dependencyName, McpDependencyFailureKind.InvalidResponse);
            }

            logger.LogInformation("{DependencyName} tool {ToolName} completed successfully.", dependencyName, toolName);
            return response.Data.Clone();
        }, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, McpClientTool>> GetOrDiscoverToolsAsync(CancellationToken cancellationToken)
    {
        if (approvedTools is not null)
        {
            return approvedTools;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (approvedTools is not null)
            {
                return approvedTools;
            }

            var connectedClient = await GetOrCreateClientUnderLockAsync(cancellationToken);
            var discovered = await connectedClient.ListToolsAsync(cancellationToken: cancellationToken);
            var discoveredByName = new Dictionary<string, McpClientTool>(StringComparer.Ordinal);
            foreach (var tool in discovered)
            {
                if (!discoveredByName.TryAdd(tool.Name, tool))
                {
                    throw new McpDependencyException(dependencyName, McpDependencyFailureKind.CapabilityMismatch);
                }
            }

            approvedTools = discoveredByName
                .Where(entry => allowedToolNames.Contains(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
            logger.LogInformation(
                "{DependencyName} capability discovery cached {ToolCount} approved tools.",
                dependencyName,
                approvedTools.Count);
            return approvedTools;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<McpClient> GetOrCreateClientUnderLockAsync(CancellationToken cancellationToken)
    {
        if (client is not null)
        {
            return client;
        }

        var httpClient = httpClientFactory.CreateClient(httpClientName);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = CreateMcpEndpoint(baseUrl),
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = timeout
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

        logger.LogInformation("Connected to {DependencyName} over Streamable HTTP.", dependencyName);
        return client;
    }

    private async Task<T> ExecuteDependencyOperationAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            return await operation(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            await ResetConnectionAsync();
            throw new McpDependencyException(dependencyName, McpDependencyFailureKind.Timeout, exception);
        }
        catch (McpDependencyException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await ResetConnectionAsync();
            throw new McpDependencyException(dependencyName, McpDependencyFailureKind.Unavailable, exception);
        }
    }

    private async Task ResetConnectionAsync()
    {
        await gate.WaitAsync();
        try
        {
            approvedTools = null;
            if (client is not null)
            {
                await client.DisposeAsync();
                client = null;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private void EnsureEnabled()
    {
        if (!enabled)
        {
            throw new McpDependencyException(dependencyName, McpDependencyFailureKind.Disabled);
        }
    }

    internal static Uri CreateMcpEndpoint(string configuredBaseUrl) =>
        new($"{configuredBaseUrl.TrimEnd('/')}/mcp", UriKind.Absolute);

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            approvedTools = null;
            if (client is not null)
            {
                await client.DisposeAsync();
                client = null;
            }
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }

    private sealed record McpResponseEnvelope(bool IsSuccess, JsonElement Data, string? Error);
}
