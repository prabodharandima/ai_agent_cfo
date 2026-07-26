using System.Text.Json;
using CfoAgent.Api.Caching;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Tests.Caching;
using CfoAgent.KnowledgeFileMcpServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using KnowledgeMcpProgram = CfoAgent.KnowledgeFileMcpServer.Program;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class McpToolAdapterTests
{
    [Fact]
    public async Task SharedDiscoveryCacheAvoidsToolsListAcrossAdapterInstances()
    {
        var root = CreateKnowledgeRoot();
        var cache = new InMemoryApplicationCache();
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var firstHandler = new RecordingDelegatingHandler(factory.Server.CreateHandler());
            using var firstHttpClient = new HttpClient(firstHandler) { BaseAddress = new Uri("http://knowledge-mcp.test") };
            await using (var firstAdapter = CreateAdapter(firstHttpClient, ["list_knowledge_files"], cache))
            {
                Assert.Equal("list_knowledge_files", Assert.Single(
                    await firstAdapter.GetApprovedToolNamesAsync(null, CancellationToken.None)));
            }

            using var secondHandler = new RecordingDelegatingHandler(factory.Server.CreateHandler());
            using var secondHttpClient = new HttpClient(secondHandler) { BaseAddress = new Uri("http://knowledge-mcp.test") };
            await using (var secondAdapter = CreateAdapter(secondHttpClient, ["list_knowledge_files"], cache))
            {
                Assert.Equal("list_knowledge_files", Assert.Single(
                    await secondAdapter.GetApprovedToolNamesAsync(null, CancellationToken.None)));
            }

            Assert.Equal(1, firstHandler.ToolsListCalls);
            Assert.Equal(0, secondHandler.ToolsListCalls);
            Assert.All(cache.Requests, request => Assert.Equal(TimeSpan.FromSeconds(300), request.TimeToLive));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ChangedAllowListUsesADifferentDiscoveryCacheKey()
    {
        var root = CreateKnowledgeRoot();
        var cache = new InMemoryApplicationCache();
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var listHandler = new RecordingDelegatingHandler(factory.Server.CreateHandler());
            using var listHttpClient = new HttpClient(listHandler) { BaseAddress = new Uri("http://knowledge-mcp.test") };
            await using (var listAdapter = CreateAdapter(listHttpClient, ["list_knowledge_files"], cache))
            {
                await listAdapter.GetApprovedToolNamesAsync(null, CancellationToken.None);
            }

            using var readHandler = new RecordingDelegatingHandler(factory.Server.CreateHandler());
            using var readHttpClient = new HttpClient(readHandler) { BaseAddress = new Uri("http://knowledge-mcp.test") };
            await using (var readAdapter = CreateAdapter(readHttpClient, ["read_knowledge_file"], cache))
            {
                await readAdapter.GetApprovedToolNamesAsync(null, CancellationToken.None);
            }

            Assert.Equal(2, cache.Requests.Select(request => request.Key).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(1, listHandler.ToolsListCalls);
            Assert.Equal(1, readHandler.ToolsListCalls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CacheFailureFallsBackToToolsList()
    {
        var root = CreateKnowledgeRoot();
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache, ThrowingDistributedCache>();
        services.AddHybridCache();
        using var serviceProvider = services.BuildServiceProvider();
        var cache = new HybridApplicationCache(
            serviceProvider.GetRequiredService<HybridCache>(),
            Options.Create(new CacheOptions { Enabled = true }),
            NullLogger<HybridApplicationCache>.Instance);
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var handler = new RecordingDelegatingHandler(factory.Server.CreateHandler());
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://knowledge-mcp.test") };
            await using var adapter = CreateAdapter(httpClient, ["list_knowledge_files"], cache);

            var tools = await adapter.GetApprovedToolNamesAsync(null, CancellationToken.None);

            Assert.Equal("list_knowledge_files", Assert.Single(tools));
            Assert.Equal(1, handler.ToolsListCalls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DiscoversCachesAndInvokesApprovedTool()
    {
        var root = CreateKnowledgeRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "budget.md"), "approved budget", CancellationToken.None);
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var handler = new RecordingDelegatingHandler(factory.Server.CreateHandler());
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://knowledge-mcp.test") };
            await using var adapter = CreateAdapter(httpClient, ["list_knowledge_files", "read_knowledge_file"]);

            var firstDiscovery = await adapter.GetApprovedToolNamesAsync(null, CancellationToken.None);
            var secondDiscovery = await adapter.GetApprovedToolNamesAsync(null, CancellationToken.None);
            var arguments = new Dictionary<string, object?> { ["relativePath"] = "budget.md" };
            var result = await adapter.CallApprovedToolAsync("read_knowledge_file", arguments, CancellationToken.None);

            Assert.Equal(["list_knowledge_files", "read_knowledge_file"], firstDiscovery);
            Assert.Equal(firstDiscovery, secondDiscovery);
            Assert.Equal("approved budget", result.GetString());
            Assert.Equal(1, handler.ToolsListCalls);
            Assert.Equal(1, handler.ToolsCallCalls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GenericAdapterInvokesAnotherApprovedDiscoveredToolWithoutClientMethod()
    {
        var root = CreateKnowledgeRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "policy.md"), "policy", CancellationToken.None);
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://knowledge-mcp.test")
            });
            await using var adapter = CreateAdapter(httpClient, ["list_knowledge_files"]);

            var result = await adapter.CallApprovedToolAsync("list_knowledge_files", null, CancellationToken.None);

            var files = Assert.IsType<string[]>(result.Deserialize<string[]>());
            Assert.Equal(["policy.md"], files);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task UnapprovedDiscoveredToolIsNotExposedOrCallable()
    {
        var root = CreateKnowledgeRoot();
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://knowledge-mcp.test")
            });
            await using var adapter = CreateAdapter(httpClient, ["list_knowledge_files"]);

            var approved = await adapter.GetApprovedToolNamesAsync(null, CancellationToken.None);
            var exception = await Assert.ThrowsAsync<McpDependencyException>(() =>
                adapter.CallApprovedToolAsync(
                    "read_knowledge_file",
                    new Dictionary<string, object?> { ["relativePath"] = "anything.md" },
                    CancellationToken.None));

            Assert.Equal("list_knowledge_files", Assert.Single(approved));
            Assert.Equal(McpDependencyFailureKind.CapabilityMismatch, exception.FailureKind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RemovedConfiguredToolProducesControlledCapabilityFailure()
    {
        var root = CreateKnowledgeRoot();
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://knowledge-mcp.test")
            });
            await using var adapter = CreateAdapter(httpClient, ["removed_read_tool"]);

            var exception = await Assert.ThrowsAsync<McpDependencyException>(() =>
                adapter.CallApprovedToolAsync("removed_read_tool", null, CancellationToken.None));

            Assert.Equal(McpDependencyFailureKind.CapabilityMismatch, exception.FailureKind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ServerSchemaRejectsInvalidArgumentsAsControlledFailure()
    {
        var root = CreateKnowledgeRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "approved.md"), "approved", CancellationToken.None);
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var handler = new RecordingDelegatingHandler(factory.Server.CreateHandler());
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://knowledge-mcp.test") };
            var cache = new InMemoryApplicationCache();
            await using var adapter = CreateAdapter(httpClient, ["read_knowledge_file"], cache);

            var exception = await Assert.ThrowsAsync<McpDependencyException>(() =>
                adapter.CallApprovedToolAsync("read_knowledge_file", null, CancellationToken.None));
            var recovered = await adapter.CallApprovedToolAsync(
                "read_knowledge_file",
                new Dictionary<string, object?> { ["relativePath"] = "approved.md" },
                CancellationToken.None);

            Assert.Equal(McpDependencyFailureKind.InvalidResponse, exception.FailureKind);
            Assert.Equal("approved", recovered.GetString());
            Assert.Equal(2, handler.ToolsListCalls);
            Assert.NotEmpty(cache.RemovedKeys);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task MissingUnrelatedAllowedToolDoesNotBreakAHealthyRequiredOperation()
    {
        var root = CreateKnowledgeRoot();
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://knowledge-mcp.test")
            });
            await using var adapter = CreateAdapter(httpClient, ["list_knowledge_files", "removed_tool"]);

            var tools = await adapter.GetApprovedToolNamesAsync(["list_knowledge_files"], CancellationToken.None);
            var result = await adapter.CallApprovedToolAsync("list_knowledge_files", null, CancellationToken.None);

            Assert.Equal("list_knowledge_files", Assert.Single(tools));
            Assert.NotNull(result.Deserialize<string[]>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AdapterCallPropagatesCallerCancellation()
    {
        var root = CreateKnowledgeRoot();
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var handler = new RecordingDelegatingHandler(factory.Server.CreateHandler());
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://knowledge-mcp.test") };
            var cache = new InMemoryApplicationCache();
            await using var adapter = CreateAdapter(httpClient, ["list_knowledge_files"], cache);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                adapter.CallApprovedToolAsync("list_knowledge_files", null, cancellation.Token));

            var recovered = await adapter.CallApprovedToolAsync(
                "list_knowledge_files",
                null,
                CancellationToken.None);

            Assert.NotNull(recovered.Deserialize<string[]>());
            Assert.Equal(1, handler.ToolsListCalls);
            Assert.Single(cache.Requests);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AdapterPortDoesNotExposeMcpSdkTypes()
    {
        var exposedTypes = typeof(IMcpToolAdapter).GetMethods()
            .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))
            .Select(type => type.FullName ?? string.Empty);

        Assert.DoesNotContain(exposedTypes, name => name.StartsWith("ModelContextProtocol.", StringComparison.Ordinal));
    }

    private static McpToolAdapter CreateAdapter(
        HttpClient httpClient,
        IReadOnlyList<string> allowedTools,
        IApplicationCache? cache = null) => new(
        "Knowledge File MCP",
        McpToolAdapter.KnowledgeFilesHttpClientName,
        true,
        "http://knowledge-mcp.test",
        5,
        allowedTools,
        new SingleHttpClientFactory(httpClient),
        NullLogger<McpToolAdapter>.Instance,
        cache ?? new InMemoryApplicationCache(),
        Options.Create(new CacheOptions()));

    private static string CreateKnowledgeRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cfo-generic-mcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class SingleHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingDelegatingHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        public int ToolsListCalls { get; private set; }

        public int ToolsCallCalls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            if (body.Contains("\"method\":\"tools/list\"", StringComparison.Ordinal))
            {
                ToolsListCalls++;
            }

            if (body.Contains("\"method\":\"tools/call\"", StringComparison.Ordinal))
            {
                ToolsCallCalls++;
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("Cache unavailable.");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Cache unavailable.");

        public void Refresh(string key) => throw new InvalidOperationException("Cache unavailable.");

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Cache unavailable.");

        public void Remove(string key) => throw new InvalidOperationException("Cache unavailable.");

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Cache unavailable.");

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new InvalidOperationException("Cache unavailable.");

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default) =>
            throw new InvalidOperationException("Cache unavailable.");
    }

    private sealed class KnowledgeFactory(string root) : WebApplicationFactory<KnowledgeMcpProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(KnowledgeRoot.ConfigurationKey, root);
        }
    }
}
