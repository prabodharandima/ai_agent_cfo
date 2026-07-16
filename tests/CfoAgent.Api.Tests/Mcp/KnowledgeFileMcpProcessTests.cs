using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class KnowledgeFileMcpProcessTests
{
    [Fact]
    public async Task DiscoversOnlyRequiredReadOnlyCapabilities()
    {
        await using var fixture = await ProcessFixture.CreateAsync();

        var tools = await fixture.ProcessClient.DiscoverToolsAsync(CancellationToken.None);

        Assert.Equal(["list_knowledge_files", "read_knowledge_file"], tools);
    }

    [Fact]
    public async Task ListsAllowedFilesThroughMcp()
    {
        await using var fixture = await ProcessFixture.CreateAsync();
        Directory.CreateDirectory(Path.Combine(fixture.KnowledgeRoot, "reports"));
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "budget.md"), "budget");
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "reports", "annual.md"), "annual");

        var files = await fixture.ProcessClient.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["budget.md", "reports/annual.md"], files);
    }

    [Fact]
    public async Task ReadsAllowedFileThroughMcp()
    {
        await using var fixture = await ProcessFixture.CreateAsync();
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "target.md"), "approved target");

        var content = await fixture.ProcessClient.ReadFileAsync("target.md", CancellationToken.None);

        Assert.Equal("approved target", content);
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("..\\outside.md")]
    public async Task ProcessClientRejectsPathsOutsideKnowledgeRoot(string path)
    {
        await using var fixture = await ProcessFixture.CreateAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.ProcessClient.ReadFileAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task MissingCapabilityUsesExistingInProcessFallback()
    {
        await using var fixture = await AccessFixture.CreateAsync(processEnabled: true);
        fixture.ProcessClient.List = _ => throw new InvalidOperationException("Required capability is missing.");
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "fallback.md"), "fallback");

        var files = await fixture.Access.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["fallback.md"], files);
        Assert.Equal(1, fixture.ProcessClient.ListCalls);
    }

    [Fact]
    public async Task UnavailableProcessUsesExistingInProcessFallback()
    {
        await using var fixture = await AccessFixture.CreateAsync(processEnabled: true);
        fixture.ProcessClient.List = _ => throw new IOException("The MCP process is unavailable.");
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "fallback.md"), "fallback");

        var files = await fixture.Access.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["fallback.md"], files);
        Assert.Equal(1, fixture.ProcessClient.ListCalls);
    }

    [Fact]
    public async Task ProcessTimeoutUsesExistingInProcessFallback()
    {
        await using var fixture = await AccessFixture.CreateAsync(processEnabled: true);
        fixture.ProcessClient.List = _ => throw new OperationCanceledException("MCP request timed out.");
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "fallback.md"), "fallback");

        var files = await fixture.Access.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["fallback.md"], files);
        Assert.Equal(1, fixture.ProcessClient.ListCalls);
    }

    [Fact]
    public async Task CallerCancellationPropagatesWithoutFallback()
    {
        await using var fixture = await AccessFixture.CreateAsync(processEnabled: true);
        fixture.ProcessClient.List = token =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Access.ListFilesAsync(cancellation.Token));

        Assert.Equal(1, fixture.ProcessClient.ListCalls);
    }

    [Fact]
    public async Task DisabledMcpUsesExistingInProcessFallbackWithoutStartingProcess()
    {
        await using var fixture = await AccessFixture.CreateAsync(processEnabled: false);
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "local.md"), "local");

        var files = await fixture.Access.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["local.md"], files);
        Assert.Equal(0, fixture.ProcessClient.ListCalls);
    }

    private sealed class ProcessFixture : IAsyncDisposable
    {
        private readonly string root;

        private ProcessFixture(string root, string knowledgeRoot, KnowledgeFileMcpProcessClient processClient)
        {
            this.root = root;
            KnowledgeRoot = knowledgeRoot;
            ProcessClient = processClient;
        }

        public string KnowledgeRoot { get; }

        public KnowledgeFileMcpProcessClient ProcessClient { get; }

        public static Task<ProcessFixture> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), $"cfo-knowledge-mcp-{Guid.NewGuid():N}");
            var knowledgeRoot = Path.Combine(root, "data", "knowledge");
            Directory.CreateDirectory(knowledgeRoot);
            var options = Options.Create(CreateOptions(knowledgeRoot, enabled: true, timeoutSeconds: 15));
            var processClient = new KnowledgeFileMcpProcessClient(
                options,
                new TestHostEnvironment(FindRepositoryRoot()),
                NullLogger<KnowledgeFileMcpProcessClient>.Instance);
            return Task.FromResult(new ProcessFixture(root, knowledgeRoot, processClient));
        }

        public async ValueTask DisposeAsync()
        {
            await ProcessClient.DisposeAsync();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class AccessFixture : IAsyncDisposable
    {
        private readonly string root;

        private AccessFixture(
            string root,
            string knowledgeRoot,
            StubKnowledgeFileMcpProcessClient processClient,
            KnowledgeFileMcpAccess access)
        {
            this.root = root;
            KnowledgeRoot = knowledgeRoot;
            ProcessClient = processClient;
            Access = access;
        }

        public string KnowledgeRoot { get; }

        public StubKnowledgeFileMcpProcessClient ProcessClient { get; }

        public KnowledgeFileMcpAccess Access { get; }

        public static Task<AccessFixture> CreateAsync(bool processEnabled)
        {
            var root = Path.Combine(Path.GetTempPath(), $"cfo-knowledge-fallback-{Guid.NewGuid():N}");
            var knowledgeRoot = Path.Combine(root, "data", "knowledge");
            Directory.CreateDirectory(knowledgeRoot);
            var options = Options.Create(CreateOptions(knowledgeRoot, processEnabled, timeoutSeconds: 1));
            var environment = new TestHostEnvironment(root);
            var processClient = new StubKnowledgeFileMcpProcessClient();
            var localClient = new KnowledgeFileMcpClient(options, environment, NullLogger<KnowledgeFileMcpClient>.Instance);
            var fallback = new KnowledgeFileMcpFallback(options, NullLogger<KnowledgeFileMcpFallback>.Instance);
            var access = new KnowledgeFileMcpAccess(processClient, localClient, fallback);
            return Task.FromResult(new AccessFixture(root, knowledgeRoot, processClient, access));
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

    private sealed class StubKnowledgeFileMcpProcessClient : IKnowledgeFileMcpProcessClient
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
        UseLocalFallback = true,
        KnowledgeFiles = new KnowledgeFileMcpOptions
        {
            Enabled = enabled,
            RootPath = rootPath,
            TimeoutSeconds = timeoutSeconds
        }
    };

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CfoAgent.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root could not be located for the MCP process test.");
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "CfoAgent.Api.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
