using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CfoAgent.Api.Caching;
using CfoAgent.Api.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Diagnostics;
using CfoAgent.Api.Observability;
using Microsoft.Extensions.Options;

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
    private readonly IApplicationCache cache;
    private readonly McpDiscoveryCacheOptions discoveryCacheOptions;
    private readonly string discoveryCacheKey;
    private readonly SemaphoreSlim gate = new(1, 1);
    private McpClient? client;
    private IReadOnlySet<string>? approvedToolNames;
    private bool forceDiscoveryRefresh;
    private bool disposed;

    public McpToolAdapter(
        string dependencyName,
        string httpClientName,
        bool enabled,
        string baseUrl,
        int timeoutSeconds,
        IEnumerable<string> allowedToolNames,
        IHttpClientFactory httpClientFactory,
        ILogger<McpToolAdapter> logger,
        IApplicationCache cache,
        IOptions<CacheOptions> cacheOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(httpClientName);
        ArgumentNullException.ThrowIfNull(allowedToolNames);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(cacheOptions);

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
        this.cache = cache;
        discoveryCacheOptions = cacheOptions.Value.McpDiscovery;
        if (discoveryCacheOptions.TtlSeconds <= 0 || string.IsNullOrWhiteSpace(discoveryCacheOptions.SchemaVersion))
        {
            throw new ArgumentException("MCP discovery cache TTL and schema version are required.", nameof(cacheOptions));
        }

        discoveryCacheKey = CreateDiscoveryCacheKey(
            dependencyName,
            baseUrl,
            this.allowedToolNames,
            discoveryCacheOptions.SchemaVersion);
    }

    public Task<IReadOnlyList<string>> GetApprovedToolNamesAsync(
        IEnumerable<string>? requiredToolNames,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return ExecuteDependencyOperationAsync(async token =>
        {
            var tools = await GetOrDiscoverToolNamesAsync(token);
            if (requiredToolNames is null)
            {
                return (IReadOnlyList<string>)tools.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            }

            var requestedNames = requiredToolNames.ToHashSet(StringComparer.Ordinal);
            if (requestedNames.Count == 0 || requestedNames.Any(name => !allowedToolNames.Contains(name)))
            {
                throw new McpDependencyException(dependencyName, McpDependencyFailureKind.CapabilityMismatch);
            }

            if (requestedNames.Any(name => !tools.Contains(name)))
            {
                tools = await RefreshDiscoveryAsync(token);
                if (requestedNames.Any(name => !tools.Contains(name)))
                {
                    throw new McpDependencyException(dependencyName, McpDependencyFailureKind.CapabilityMismatch);
                }
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
            if (!allowedToolNames.Contains(toolName))
            {
                throw new McpDependencyException(dependencyName, McpDependencyFailureKind.CapabilityMismatch);
            }

            var tools = await GetOrDiscoverToolNamesAsync(token);
            if (!tools.Contains(toolName))
            {
                tools = await RefreshDiscoveryAsync(token);
                if (!tools.Contains(toolName))
                {
                    throw new McpDependencyException(dependencyName, McpDependencyFailureKind.CapabilityMismatch);
                }
            }

            var connectedClient = await GetOrCreateClientAsync(token);
            var result = await connectedClient.CallToolAsync(toolName, arguments, cancellationToken: token);
            if (result.IsError == true)
            {
                await ResetConnectionAsync(invalidateDistributedDiscovery: true);
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

    private async Task<IReadOnlySet<string>> GetOrDiscoverToolNamesAsync(CancellationToken cancellationToken)
    {
        if (approvedToolNames is not null)
        {
            return approvedToolNames;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (approvedToolNames is not null)
            {
                return approvedToolNames;
            }

            var connectedClient = await GetOrCreateClientUnderLockAsync(cancellationToken);
            string[] cachedNames;
            if (forceDiscoveryRefresh)
            {
                cachedNames = await DiscoverApprovedToolNamesAsync(connectedClient, cancellationToken);
                forceDiscoveryRefresh = false;
                await cache.GetOrCreateAsync(
                    discoveryCacheKey,
                    TimeSpan.FromSeconds(discoveryCacheOptions.TtlSeconds),
                    _ => Task.FromResult(cachedNames),
                    cancellationToken);
            }
            else
            {
                cachedNames = await cache.GetOrCreateAsync(
                    discoveryCacheKey,
                    TimeSpan.FromSeconds(discoveryCacheOptions.TtlSeconds),
                    token => DiscoverApprovedToolNamesAsync(connectedClient, token),
                    cancellationToken);
            }

            approvedToolNames = cachedNames
                .Where(allowedToolNames.Contains)
                .ToHashSet(StringComparer.Ordinal);
            logger.LogInformation(
                "{DependencyName} capability discovery loaded {ToolCount} approved tools.",
                dependencyName,
                approvedToolNames.Count);
            return approvedToolNames;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string[]> DiscoverApprovedToolNamesAsync(
        McpClient connectedClient,
        CancellationToken cancellationToken)
    {
        var discovered = await connectedClient.ListToolsAsync(cancellationToken: cancellationToken);
        var discoveredNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in discovered)
        {
            if (!discoveredNames.Add(tool.Name))
            {
                throw new McpDependencyException(dependencyName, McpDependencyFailureKind.CapabilityMismatch);
            }
        }

        return discoveredNames
            .Where(allowedToolNames.Contains)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlySet<string>> RefreshDiscoveryAsync(CancellationToken cancellationToken)
    {
        await InvalidateDiscoveryAsync(cancellationToken);
        return await GetOrDiscoverToolNamesAsync(cancellationToken);
    }

    private async Task InvalidateDiscoveryAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            approvedToolNames = null;
            forceDiscoveryRefresh = true;
        }
        finally
        {
            gate.Release();
        }

        await cache.RemoveAsync(discoveryCacheKey, cancellationToken);
    }

    private async Task<McpClient> GetOrCreateClientAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return await GetOrCreateClientUnderLockAsync(cancellationToken);
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
        var operationName = string.Equals(dependencyName, "Finance MCP", StringComparison.Ordinal)
            ? "finance-mcp.operation"
            : "knowledge-mcp.operation";
        var activity = AgentActivityTracing.Start(operationName);
        var stopwatch = Stopwatch.StartNew();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            var result = await operation(timeoutSource.Token);
            AgentActivityTracing.Complete(activity, operationName, stopwatch, "Success");
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AgentActivityTracing.Complete(activity, operationName, stopwatch, "Cancelled");
            throw;
        }
        catch (OperationCanceledException exception)
        {
            await ResetConnectionAsync(invalidateDistributedDiscovery: true);
            AgentActivityTracing.Complete(activity, operationName, stopwatch, "Failure");
            throw new McpDependencyException(dependencyName, McpDependencyFailureKind.Timeout, exception);
        }
        catch (McpDependencyException)
        {
            AgentActivityTracing.Complete(activity, operationName, stopwatch, "Failure");
            throw;
        }
        catch (Exception exception)
        {
            await ResetConnectionAsync(invalidateDistributedDiscovery: true);
            AgentActivityTracing.Complete(activity, operationName, stopwatch, "Failure");
            throw new McpDependencyException(dependencyName, McpDependencyFailureKind.Unavailable, exception);
        }
    }

    private async Task ResetConnectionAsync(bool invalidateDistributedDiscovery)
    {
        await gate.WaitAsync();
        try
        {
            approvedToolNames = null;
            forceDiscoveryRefresh = invalidateDistributedDiscovery;
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

        if (invalidateDistributedDiscovery)
        {
            await cache.RemoveAsync(discoveryCacheKey, CancellationToken.None);
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

    internal static string CreateDiscoveryCacheKey(
        string dependencyName,
        string configuredBaseUrl,
        IEnumerable<string> allowedToolNames,
        string schemaVersion)
    {
        var canonicalAllowList = string.Join('\n', allowedToolNames.OrderBy(name => name, StringComparer.Ordinal));
        return string.Join(
            ':',
            "mcp-discovery",
            Sha256(schemaVersion),
            Sha256(dependencyName),
            Sha256(CreateMcpEndpoint(configuredBaseUrl).AbsoluteUri),
            Sha256(canonicalAllowList));
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

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
            approvedToolNames = null;
            forceDiscoveryRefresh = false;
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
