using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using CfoAgent.KnowledgeFileMcpServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using KnowledgeMcpProgram = CfoAgent.KnowledgeFileMcpServer.Program;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class KnowledgeFileMcpHttpHostTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string root = Path.Combine(Path.GetTempPath(), $"cfo-knowledge-http-{Guid.NewGuid():N}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(root);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task StreamableHttpDiscoveryReturnsExactlyTwoReadOnlyTools()
    {
        await using var server = await KnowledgeMcpHttpServer.CreateAsync(root);

        var tools = (await server.McpClient.ListToolsAsync(cancellationToken: CancellationToken.None))
            .Select(tool => tool.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["list_knowledge_files", "read_knowledge_file"], tools);
    }

    [Fact]
    public async Task ListAndReadWorkOverHttpWithNormalizedRelativePaths()
    {
        Directory.CreateDirectory(Path.Combine(root, "reports"));
        await File.WriteAllTextAsync(Path.Combine(root, "budget.md"), "budget", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(root, "reports", "annual.md"), "annual", CancellationToken.None);
        await using var server = await KnowledgeMcpHttpServer.CreateAsync(root);

        var files = await CallAsync<KnowledgeFileMcpResult<string[]>>(
            server.McpClient,
            "list_knowledge_files",
            null,
            CancellationToken.None);
        var content = await CallAsync<KnowledgeFileMcpResult<string>>(
            server.McpClient,
            "read_knowledge_file",
            new Dictionary<string, object?> { ["relativePath"] = "reports/annual.md" },
            CancellationToken.None);

        Assert.True(files.IsSuccess);
        Assert.NotNull(files.Data);
        Assert.Equal(["budget.md", "reports/annual.md"], files.Data);
        Assert.True(content.IsSuccess);
        Assert.Equal("annual", content.Data);
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("..\\outside.md")]
    public async Task HttpServerRejectsPathTraversalWithoutLeakingRoot(string path)
    {
        await using var server = await KnowledgeMcpHttpServer.CreateAsync(root);

        var result = await server.McpClient.CallToolAsync(
            "read_knowledge_file",
            new Dictionary<string, object?> { ["relativePath"] = path },
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsError);
        Assert.DoesNotContain(root, GetText(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HttpServerRejectsAbsolutePathsWithoutLeakingRoot()
    {
        await using var server = await KnowledgeMcpHttpServer.CreateAsync(root);

        var result = await server.McpClient.CallToolAsync(
            "read_knowledge_file",
            new Dictionary<string, object?> { ["relativePath"] = Path.Combine(root, "budget.md") },
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsError);
        Assert.DoesNotContain(root, GetText(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingFileReturnsTypedFailure()
    {
        await using var server = await KnowledgeMcpHttpServer.CreateAsync(root);

        var result = await CallAsync<KnowledgeFileMcpResult<string>>(
            server.McpClient,
            "read_knowledge_file",
            new Dictionary<string, object?> { ["relativePath"] = "missing.md" },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Contains("does not exist", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationIsPropagated()
    {
        await using var server = await KnowledgeMcpHttpServer.CreateAsync(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await server.McpClient.CallToolAsync(
            "list_knowledge_files",
            cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task HealthReportsReadyWithoutDisclosingTheConfiguredRoot()
    {
        await using var server = await KnowledgeMcpHttpServer.CreateWithoutMcpClient(root);

        var live = await server.HttpClient.GetAsync("/health/live", CancellationToken.None);
        var ready = await server.HttpClient.GetAsync("/health/ready", CancellationToken.None);
        var body = await ready.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.DoesNotContain(root, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadinessBecomesUnhealthyWhenTheConfiguredRootDisappears()
    {
        await using var server = await KnowledgeMcpHttpServer.CreateWithoutMcpClient(root);
        Directory.Delete(root, recursive: true);

        var live = await server.HttpClient.GetAsync("/health/live", CancellationToken.None);
        var ready = await server.HttpClient.GetAsync("/health/ready", CancellationToken.None);
        var body = await ready.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.DoesNotContain(root, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IndependentKestrelHostUsesEnvironmentConfiguredRootAndUrl()
    {
        var port = GetAvailablePort();
        var assemblyPath = Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "CfoAgent.KnowledgeFileMcpServer",
            "bin",
            GetBuildConfiguration(),
            "net10.0",
            "CfoAgent.KnowledgeFileMcpServer.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.Environment[KnowledgeRoot.EnvironmentVariableName] = root;
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Testing";

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The Knowledge File MCP HTTP host could not be started.");
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            HttpResponseMessage? response = null;
            while (!timeout.IsCancellationRequested)
            {
                try
                {
                    response = await httpClient.GetAsync("/health/ready", timeout.Token);
                    break;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(100, timeout.Token);
                }
            }

            Assert.NotNull(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static async Task<T> CallAsync<T>(
        McpClient client,
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken)
    {
        var call = await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken);
        var content = Assert.IsType<TextContentBlock>(Assert.Single(call.Content));
        return JsonSerializer.Deserialize<T>(content.Text, JsonOptions)
            ?? throw new JsonException($"Tool {toolName} returned an empty result.");
    }

    private static string GetText(CallToolResult result) =>
        string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CfoAgent.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root could not be located.");
    }

    private static string GetBuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private sealed class KnowledgeMcpHttpServer : IAsyncDisposable
    {
        private readonly WebApplicationFactory<KnowledgeMcpProgram> factory;

        private KnowledgeMcpHttpServer(
            WebApplicationFactory<KnowledgeMcpProgram> factory,
            HttpClient httpClient,
            McpClient? mcpClient)
        {
            this.factory = factory;
            HttpClient = httpClient;
            McpClient = mcpClient!;
        }

        public HttpClient HttpClient { get; }

        public McpClient McpClient { get; }

        public static async Task<KnowledgeMcpHttpServer> CreateAsync(string root)
        {
            var server = await CreateWithoutMcpClient(root);
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(server.HttpClient.BaseAddress!, "/mcp"),
                    TransportMode = HttpTransportMode.StreamableHttp,
                    ConnectionTimeout = TimeSpan.FromSeconds(10)
                },
                server.HttpClient,
                NullLoggerFactory.Instance,
                ownsHttpClient: false);
            var mcpClient = await ModelContextProtocol.Client.McpClient.CreateAsync(
                transport,
                cancellationToken: CancellationToken.None);
            return new KnowledgeMcpHttpServer(server.factory, server.HttpClient, mcpClient);
        }

        public static Task<KnowledgeMcpHttpServer> CreateWithoutMcpClient(string root)
        {
            var factory = new KnowledgeMcpWebApplicationFactory(root);
            var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://knowledge-mcp.test")
            });
            return Task.FromResult(new KnowledgeMcpHttpServer(factory, httpClient, null));
        }

        public async ValueTask DisposeAsync()
        {
            if (McpClient is not null)
            {
                await McpClient.DisposeAsync();
            }

            HttpClient.Dispose();
            await factory.DisposeAsync();
        }
    }

    private sealed class KnowledgeMcpWebApplicationFactory(string root) : WebApplicationFactory<KnowledgeMcpProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(KnowledgeRoot.ConfigurationKey, root);
        }
    }
}
