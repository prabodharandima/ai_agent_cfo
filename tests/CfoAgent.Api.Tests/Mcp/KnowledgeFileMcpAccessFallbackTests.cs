using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class KnowledgeFileMcpAccessFallbackTests
{
    [Theory]
    [InlineData(McpDependencyFailureKind.CapabilityMismatch, "capability-mismatch")]
    [InlineData(McpDependencyFailureKind.Unavailable, "unavailable")]
    [InlineData(McpDependencyFailureKind.Timeout, "timeout")]
    public async Task RemoteFailureUsesExistingInProcessFallbackAndLogsReason(
        McpDependencyFailureKind failureKind,
        string expectedReason)
    {
        await using var fixture = await AccessFixture.CreateAsync(remoteEnabled: true);
        fixture.RemoteClient.List = _ => throw new McpDependencyException(
            "Knowledge File MCP",
            failureKind);
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "fallback.md"), "fallback");

        var files = await fixture.Access.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["fallback.md"], files);
        Assert.Equal(1, fixture.RemoteClient.ListCalls);
        Assert.Contains(fixture.LogMessages, message => message.Contains(expectedReason, StringComparison.Ordinal));
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
    public async Task ReadFileUsesExistingInProcessFallback()
    {
        await using var fixture = await AccessFixture.CreateAsync(remoteEnabled: true);
        fixture.RemoteClient.Read = (_, _) => throw new McpDependencyException(
            "Knowledge File MCP",
            McpDependencyFailureKind.Unavailable);
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "fallback.md"), "fallback");

        var content = await fixture.Access.ReadFileAsync("fallback.md", CancellationToken.None);

        Assert.Equal("fallback", content);
        Assert.Equal(1, fixture.RemoteClient.ReadCalls);
    }

    [Fact]
    public async Task DisabledMcpUsesExistingInProcessFallbackWithoutContactingEndpoint()
    {
        await using var fixture = await AccessFixture.CreateAsync(remoteEnabled: false);
        await File.WriteAllTextAsync(Path.Combine(fixture.KnowledgeRoot, "local.md"), "local");

        var files = await fixture.Access.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["local.md"], files);
        Assert.Equal(0, fixture.RemoteClient.ListCalls);
        Assert.Contains(fixture.LogMessages, message => message.Contains("disabled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DisabledMcpWithoutDevelopmentFallbackFailsWithoutContactingEndpoint()
    {
        await using var fixture = await AccessFixture.CreateAsync(remoteEnabled: false, useLocalFallback: false);

        var exception = await Assert.ThrowsAsync<McpDependencyException>(() =>
            fixture.Access.ListFilesAsync(CancellationToken.None));

        Assert.Equal(McpDependencyFailureKind.Disabled, exception.FailureKind);
        Assert.Equal(0, fixture.RemoteClient.ListCalls);
    }

    [Fact]
    public async Task RemoteFailureDoesNotUseLocalFallbackOutsideDevelopment()
    {
        await using var fixture = await AccessFixture.CreateAsync(
            remoteEnabled: true,
            environmentName: Environments.Production);
        fixture.RemoteClient.List = _ => throw new McpDependencyException(
            "Knowledge File MCP",
            McpDependencyFailureKind.Unavailable);

        await Assert.ThrowsAsync<McpDependencyException>(() => fixture.Access.ListFilesAsync(CancellationToken.None));

        Assert.Equal(1, fixture.RemoteClient.ListCalls);
    }

    private sealed class AccessFixture : IAsyncDisposable
    {
        private readonly string root;

        private AccessFixture(
            string root,
            string knowledgeRoot,
            StubKnowledgeFileMcpRemoteClient remoteClient,
            KnowledgeFileMcpAccess access,
            RecordingLogger<KnowledgeFileMcpAccess> logger)
        {
            this.root = root;
            KnowledgeRoot = knowledgeRoot;
            RemoteClient = remoteClient;
            Access = access;
            LogMessages = logger.Messages;
        }

        public string KnowledgeRoot { get; }

        public StubKnowledgeFileMcpRemoteClient RemoteClient { get; }

        public KnowledgeFileMcpAccess Access { get; }

        public IReadOnlyList<string> LogMessages { get; }

        public static Task<AccessFixture> CreateAsync(
            bool remoteEnabled,
            bool useLocalFallback = true,
            string environmentName = "Development")
        {
            var root = Path.Combine(Path.GetTempPath(), $"cfo-knowledge-fallback-{Guid.NewGuid():N}");
            var knowledgeRoot = Path.Combine(root, "data", "knowledge");
            Directory.CreateDirectory(knowledgeRoot);
            var options = Options.Create(CreateOptions(knowledgeRoot, remoteEnabled, useLocalFallback, timeoutSeconds: 1));
            var environment = new TestHostEnvironment(root) { EnvironmentName = environmentName };
            var remoteClient = new StubKnowledgeFileMcpRemoteClient();
            var localClient = new KnowledgeFileMcpClient(options, environment, NullLogger<KnowledgeFileMcpClient>.Instance);
            var logger = new RecordingLogger<KnowledgeFileMcpAccess>();
            var access = new KnowledgeFileMcpAccess(remoteClient, localClient, options, environment, logger);
            return Task.FromResult(new AccessFixture(root, knowledgeRoot, remoteClient, access, logger));
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

        public Func<string, CancellationToken, Task<string>> Read { get; set; } =
            (_, _) => Task.FromResult(string.Empty);

        public int ReadCalls { get; private set; }

        public Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(["list_knowledge_files", "read_knowledge_file"]);

        public Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            return List(cancellationToken);
        }

        public Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken)
        {
            ReadCalls++;
            return Read(relativePath, cancellationToken);
        }
    }

    private static McpOptions CreateOptions(string rootPath, bool enabled, bool useLocalFallback, int timeoutSeconds) => new()
    {
        KnowledgeFiles = new KnowledgeFileMcpOptions
        {
            Enabled = enabled,
            BaseUrl = "http://knowledge-mcp.test",
            RootPath = rootPath,
            UseLocalFallback = useLocalFallback,
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

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
