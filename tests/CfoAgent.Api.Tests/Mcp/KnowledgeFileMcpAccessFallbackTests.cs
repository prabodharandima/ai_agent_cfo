using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class KnowledgeFileMcpAccessFallbackTests
{
    [Fact]
    public async Task MissingCapabilityUsesExistingInProcessFallback()
    {
        await using var fixture = await AccessFixture.CreateAsync(remoteEnabled: true);
        fixture.RemoteClient.List = _ => throw new McpDependencyException(
            "Knowledge File MCP",
            McpDependencyFailureKind.CapabilityMismatch);
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "fallback.md"), "fallback");

        var files = await fixture.Access.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["fallback.md"], files);
        Assert.Equal(1, fixture.RemoteClient.ListCalls);
    }

    [Fact]
    public async Task UnavailableEndpointUsesExistingInProcessFallback()
    {
        await using var fixture = await AccessFixture.CreateAsync(remoteEnabled: true);
        fixture.RemoteClient.List = _ => throw new McpDependencyException(
            "Knowledge File MCP",
            McpDependencyFailureKind.Unavailable);
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "fallback.md"), "fallback");

        var files = await fixture.Access.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["fallback.md"], files);
        Assert.Equal(1, fixture.RemoteClient.ListCalls);
    }

    [Fact]
    public async Task EndpointTimeoutUsesExistingInProcessFallback()
    {
        await using var fixture = await AccessFixture.CreateAsync(remoteEnabled: true);
        fixture.RemoteClient.List = _ => throw new McpDependencyException(
            "Knowledge File MCP",
            McpDependencyFailureKind.Timeout);
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "fallback.md"), "fallback");

        var files = await fixture.Access.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["fallback.md"], files);
        Assert.Equal(1, fixture.RemoteClient.ListCalls);
    }

    [Fact]
    public async Task CallerCancellationPropagatesWithoutFallback()
    {
        await using var fixture = await AccessFixture.CreateAsync(remoteEnabled: true);
        fixture.RemoteClient.List = token =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Access.ListFilesAsync(cancellation.Token));

        Assert.Equal(1, fixture.RemoteClient.ListCalls);
    }

    [Fact]
    public async Task DisabledMcpUsesExistingInProcessFallbackWithoutContactingEndpoint()
    {
        await using var fixture = await AccessFixture.CreateAsync(remoteEnabled: false);
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "local.md"), "local");

        var files = await fixture.Access.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["local.md"], files);
        Assert.Equal(0, fixture.RemoteClient.ListCalls);
    }

    private sealed class AccessFixture : IAsyncDisposable
    {
        private readonly string root;

        private AccessFixture(
            string root,
            string knowledgeRoot,
            StubKnowledgeFileMcpRemoteClient remoteClient,
            KnowledgeFileMcpAccess access)
        {
            this.root = root;
            KnowledgeRoot = knowledgeRoot;
            RemoteClient = remoteClient;
            Access = access;
        }

        public string KnowledgeRoot { get; }

        public StubKnowledgeFileMcpRemoteClient RemoteClient { get; }

        public KnowledgeFileMcpAccess Access { get; }

        public static Task<AccessFixture> CreateAsync(bool remoteEnabled)
        {
            var root = Path.Combine(Path.GetTempPath(), $"cfo-knowledge-fallback-{Guid.NewGuid():N}");
            var knowledgeRoot = Path.Combine(root, "data", "knowledge");
            Directory.CreateDirectory(knowledgeRoot);
            var options = Options.Create(CreateOptions(knowledgeRoot, remoteEnabled, timeoutSeconds: 1));
            var environment = new TestHostEnvironment(root);
            var remoteClient = new StubKnowledgeFileMcpRemoteClient();
            var localClient = new KnowledgeFileMcpClient(options, environment, NullLogger<KnowledgeFileMcpClient>.Instance);
            var fallback = new KnowledgeFileMcpFallback(options, environment, NullLogger<KnowledgeFileMcpFallback>.Instance);
            var access = new KnowledgeFileMcpAccess(remoteClient, localClient, fallback);
            return Task.FromResult(new AccessFixture(root, knowledgeRoot, remoteClient, access));
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubKnowledgeFileMcpRemoteClient : IKnowledgeFileMcpRemoteClient
    {
        public Func<CancellationToken, Task<IReadOnlyList<string>>> List { get; set; } =
            _ => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public int ListCalls { get; private set; }

        public Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(["list_knowledge_files", "read_knowledge_file"]);

        public Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            return List(cancellationToken);
        }

        public Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static McpOptions CreateOptions(string rootPath, bool enabled, int timeoutSeconds) => new()
    {
        KnowledgeFiles = new KnowledgeFileMcpOptions
        {
            Enabled = enabled,
            BaseUrl = "http://knowledge-mcp.test",
            RootPath = rootPath,
            UseLocalFallback = true,
            TimeoutSeconds = timeoutSeconds
        }
    };

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "CfoAgent.Api.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
