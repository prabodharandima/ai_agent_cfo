using System.Diagnostics;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Tests.Finance;
using CfoAgent.FinanceMcpServer.Configuration;
using CfoAgent.KnowledgeFileMcpServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using FinanceMcpProgram = CfoAgent.FinanceMcpServer.Program;
using KnowledgeMcpProgram = CfoAgent.KnowledgeFileMcpServer.Program;

namespace CfoAgent.Api.Tests.Mcp;

[Collection(FinancePostgreSqlCollection.Name)]
public sealed class ApiHttpMcpClientTests(FinancePostgreSqlFixture postgres)
{
    [Fact]
    public async Task FinanceClientDiscoversAndCallsApprovedToolsOverStreamableHttp()
    {
        await using var factory = new FinanceFactory(postgres.ConnectionString);
        using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://finance-mcp.test")
        });
        await using var client = CreateFinanceClient(httpClient);

        var tools = await client.DiscoverToolsAsync(CancellationToken.None);
        var summary = await client.GetCurrentWeekSummaryAsync(CancellationToken.None);

        Assert.Equal(
            ["compare_sales_periods", "get_budget_target", "get_historical_sales", "get_sales_summary", "get_top_products"],
            tools);
        Assert.Equal(new DateOnly(2026, 7, 13), summary.Period.StartDate);
        Assert.Equal(new DateOnly(2026, 7, 15), summary.Period.EndDate);
        Assert.True(summary.NetRevenue > 0m);
    }

    [Fact]
    public async Task FinanceClientRejectsCapabilityMismatch()
    {
        var root = CreateKnowledgeRoot();
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://knowledge-mcp.test")
            });
            await using var client = CreateFinanceClient(httpClient);

            var exception = await Assert.ThrowsAsync<McpDependencyException>(() =>
                client.DiscoverToolsAsync(CancellationToken.None));

            Assert.Equal(McpDependencyFailureKind.CapabilityMismatch, exception.FailureKind);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task KnowledgeClientListsAndReadsOnlyAllowedPathsOverStreamableHttp()
    {
        var root = CreateKnowledgeRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "budget.md"), "approved budget", CancellationToken.None);
        try
        {
            await using var factory = new KnowledgeFactory(root);
            using var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://knowledge-mcp.test")
            });
            await using var client = CreateKnowledgeClient(httpClient);

            var tools = await client.DiscoverToolsAsync(CancellationToken.None);
            var files = await client.ListFilesAsync(CancellationToken.None);
            var content = await client.ReadFileAsync("budget.md", CancellationToken.None);

            Assert.Equal(["list_knowledge_files", "read_knowledge_file"], tools);
            Assert.Equal(["budget.md"], files);
            Assert.Equal("approved budget", content);
            await Assert.ThrowsAsync<ArgumentException>(() => client.ReadFileAsync("../outside.md", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DisabledFinanceClientDoesNotSendNetworkRequests()
    {
        var handler = new CountingHandler();
        using var httpClient = new HttpClient(handler);
        await using var client = CreateFinanceClient(httpClient, enabled: false);

        var exception = await Assert.ThrowsAsync<McpDependencyException>(() =>
            client.GetCurrentWeekSummaryAsync(CancellationToken.None));

        Assert.Equal(McpDependencyFailureKind.Disabled, exception.FailureKind);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ClientConstructionDoesNotSendNetworkRequests()
    {
        var handler = new CountingHandler();
        using var httpClient = new HttpClient(handler);

        await using var finance = CreateFinanceClient(httpClient);

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public void ApiMcpConfigurationAndClientsContainNoChildProcessContract()
    {
        Assert.Null(typeof(FinanceMcpOptions).GetProperty("ServerProjectPath"));
        Assert.DoesNotContain(typeof(IHostEnvironment), typeof(FinanceMcpClient).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType));
        Assert.DoesNotContain(typeof(IHostEnvironment), typeof(KnowledgeFileMcpHttpClient).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public async Task UnavailableFinanceEndpointProducesSanitizedDependencyFailure()
    {
        using var httpClient = new HttpClient(new ThrowingHandler());
        await using var client = CreateFinanceClient(httpClient);

        var exception = await Assert.ThrowsAsync<McpDependencyException>(() =>
            client.GetCurrentWeekSummaryAsync(CancellationToken.None));

        Assert.Equal(McpDependencyFailureKind.Unavailable, exception.FailureKind);
        Assert.DoesNotContain("finance-mcp.test", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinanceTimeoutProducesTimeoutDependencyFailure()
    {
        using var httpClient = new HttpClient(new DelayingHandler());
        await using var client = CreateFinanceClient(httpClient, timeoutSeconds: 1);

        var exception = await Assert.ThrowsAsync<McpDependencyException>(() =>
            client.GetCurrentWeekSummaryAsync(CancellationToken.None));

        Assert.Equal(McpDependencyFailureKind.Timeout, exception.FailureKind);
    }

    [Fact]
    public async Task CallerCancellationPropagatesWithoutDependencyConversion()
    {
        using var httpClient = new HttpClient(new DelayingHandler());
        await using var client = CreateFinanceClient(httpClient, timeoutSeconds: 10);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetCurrentWeekSummaryAsync(cancellation.Token));
    }

    private static FinanceMcpClient CreateFinanceClient(
        HttpClient httpClient,
        bool enabled = true,
        int timeoutSeconds = 5) => new(
            Options.Create(new McpOptions
            {
                Finance = new FinanceMcpOptions
                {
                    Enabled = enabled,
                    BaseUrl = "http://finance-mcp.test",
                    TimeoutSeconds = timeoutSeconds
                },
                KnowledgeFiles = CreateKnowledgeOptions()
            }),
            new SingleHttpClientFactory(httpClient),
            new FixedTimeProvider(new DateOnly(2026, 7, 15)),
            NullLogger<FinanceMcpClient>.Instance);

    private static KnowledgeFileMcpHttpClient CreateKnowledgeClient(HttpClient httpClient) => new(
        Options.Create(new McpOptions
        {
            Finance = new FinanceMcpOptions { TimeoutSeconds = 5 },
            KnowledgeFiles = CreateKnowledgeOptions(enabled: true)
        }),
        new SingleHttpClientFactory(httpClient),
        NullLogger<KnowledgeFileMcpHttpClient>.Instance);

    private static KnowledgeFileMcpOptions CreateKnowledgeOptions(bool enabled = false) => new()
    {
        Enabled = enabled,
        BaseUrl = "http://knowledge-mcp.test",
        RootPath = "unused",
        TimeoutSeconds = 5
    };

    private static string CreateKnowledgeRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cfo-api-http-mcp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class SingleHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Endpoint unavailable.");
    }

    private sealed class DelayingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        }
    }

    private sealed class FinanceFactory(string connectionString) : WebApplicationFactory<FinanceMcpProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:FinanceDatabase", connectionString);
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
