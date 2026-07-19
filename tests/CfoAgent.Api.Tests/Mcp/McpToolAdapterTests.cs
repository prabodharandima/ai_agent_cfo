using System.Text.Json;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using CfoAgent.KnowledgeFileMcpServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using KnowledgeMcpProgram = CfoAgent.KnowledgeFileMcpServer.Program;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class McpToolAdapterTests
{
    [Fact]
    public async Task DiscoversCachesPresentsAndInvokesApprovedToolSelectedByMock()
    {
        var root = CreateKnowledgeRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "budget.md"), "approved budget", CancellationToken.None);
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var handler = new RecordingDelegatingHandler(factory.Server.CreateHandler());
            using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://knowledge-mcp.test") };
            await using var adapter = CreateAdapter(httpClient, ["list_knowledge_files", "read_knowledge_file"]);

            var firstDiscovery = await adapter.GetApprovedToolsAsync(null, CancellationToken.None);
            var secondDiscovery = await adapter.GetApprovedToolsAsync(null, CancellationToken.None);
            var framework = CreateFramework(new MockChatClient(Options.Create(new AiOptions())));
            var arguments = new Dictionary<string, object?> { ["relativePath"] = "budget.md" };

            var firstSelection = await framework.SelectMcpToolAsync(
                "Knowledge File MCP",
                "Read the budget knowledge file.",
                firstDiscovery,
                arguments,
                CancellationToken.None);
            var secondSelection = await framework.SelectMcpToolAsync(
                "Knowledge File MCP",
                "Read the budget knowledge file.",
                firstDiscovery,
                arguments,
                CancellationToken.None);
            var result = await adapter.CallApprovedToolAsync(firstSelection.Name, arguments, CancellationToken.None);

            Assert.Equal(["list_knowledge_files", "read_knowledge_file"], firstDiscovery.Select(tool => tool.Name));
            Assert.Same(firstDiscovery[0], secondDiscovery[0]);
            Assert.All(firstDiscovery, tool => Assert.Equal(JsonValueKind.Object, tool.JsonSchema.ValueKind));
            Assert.Equal("read_knowledge_file", firstSelection.Name);
            Assert.Equal(firstSelection.Name, secondSelection.Name);
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

            var approved = await adapter.GetApprovedToolsAsync(null, CancellationToken.None);
            var exception = await Assert.ThrowsAsync<McpDependencyException>(() =>
                adapter.CallApprovedToolAsync(
                    "read_knowledge_file",
                    new Dictionary<string, object?> { ["relativePath"] = "anything.md" },
                    CancellationToken.None));

            Assert.Equal("list_knowledge_files", Assert.Single(approved).Name);
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
                adapter.GetApprovedToolsAsync(null, CancellationToken.None));

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
    public async Task ModelArgumentsMustMatchCanonicalArguments()
    {
        var root = CreateKnowledgeRoot();
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://knowledge-mcp.test")
            });
            await using var adapter = CreateAdapter(httpClient, ["read_knowledge_file"]);
            var tools = await adapter.GetApprovedToolsAsync(null, CancellationToken.None);
            var framework = CreateFramework(new FunctionCallChatClient(
                new Dictionary<string, object?> { ["relativePath"] = "changed.md" }));

            var exception = await Assert.ThrowsAsync<McpDependencyException>(() => framework.SelectMcpToolAsync(
                "Knowledge File MCP",
                "Read the file.",
                tools,
                new Dictionary<string, object?> { ["relativePath"] = "approved.md" },
                CancellationToken.None));

            Assert.Equal(McpDependencyFailureKind.InvalidResponse, exception.FailureKind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ToolSelectionPropagatesCallerCancellation()
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
            var tools = await adapter.GetApprovedToolsAsync(null, CancellationToken.None);
            var framework = CreateFramework(new CancellingChatClient());
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => framework.SelectMcpToolAsync(
                "Knowledge File MCP",
                "List files.",
                tools,
                new Dictionary<string, object?>(),
                cancellation.Token));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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

    private static CfoAgentFramework CreateFramework(IChatClient chatClient) => new(
        chatClient,
        NullLoggerFactory.Instance,
        new EmptyServiceProvider());

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

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
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

    private sealed class FunctionCallChatClient(IDictionary<string, object?> arguments) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var tool = Assert.Single(options?.Tools?.OfType<AIFunctionDeclaration>() ?? []);
            return Task.FromResult(new ChatResponse(new ChatMessage(
                ChatRole.Assistant,
                [new FunctionCallContent("test-call", tool.Name, arguments)])));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class CancellingChatClient : IChatClient
    {
        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable after cancellation.");
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
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
