using System.Text.Json;
using CfoAgent.Api.Mcp;
using CfoAgent.KnowledgeFileMcpServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using KnowledgeMcpProgram = CfoAgent.KnowledgeFileMcpServer.Program;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class McpToolAdapterTests
{
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
            await using var adapter = CreateAdapter(httpClient, ["read_knowledge_file"]);

            var exception = await Assert.ThrowsAsync<McpDependencyException>(() =>
                adapter.CallApprovedToolAsync("read_knowledge_file", null, CancellationToken.None));
            var recovered = await adapter.CallApprovedToolAsync(
                "read_knowledge_file",
                new Dictionary<string, object?> { ["relativePath"] = "approved.md" },
                CancellationToken.None);

            Assert.Equal(McpDependencyFailureKind.InvalidResponse, exception.FailureKind);
            Assert.Equal("approved", recovered.GetString());
            Assert.Equal(2, handler.ToolsListCalls);
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
            using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://knowledge-mcp.test")
            });
            await using var adapter = CreateAdapter(httpClient, ["list_knowledge_files"]);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                adapter.CallApprovedToolAsync("list_knowledge_files", null, cancellation.Token));
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

    private static McpToolAdapter CreateAdapter(HttpClient httpClient, IReadOnlyList<string> allowedTools) => new(
        "Knowledge File MCP",
        McpToolAdapter.KnowledgeFilesHttpClientName,
        true,
        "http://knowledge-mcp.test",
        5,
        allowedTools,
        new SingleHttpClientFactory(httpClient),
        NullLogger<McpToolAdapter>.Instance);

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

    private sealed class KnowledgeFactory(string root) : WebApplicationFactory<KnowledgeMcpProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(KnowledgeRoot.ConfigurationKey, root);
        }
    }
}
