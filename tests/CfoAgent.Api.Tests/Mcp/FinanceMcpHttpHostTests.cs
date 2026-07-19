using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using CfoAgent.Api.Tests.Finance;
using CfoAgent.FinanceMcpServer;
using CfoAgent.FinanceMcpServer.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using FinanceMcpProgram = CfoAgent.FinanceMcpServer.Program;

namespace CfoAgent.Api.Tests.Mcp;

[Collection(FinancePostgreSqlCollection.Name)]
public sealed class FinanceMcpHttpHostTests(FinancePostgreSqlFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ExpectedTools =
    [
        "compare_sales_periods",
        "get_budget_target",
        "get_historical_sales",
        "get_sales_summary",
        "get_top_products"
    ];

    [Fact]
    public async Task StreamableHttpDiscoveryReturnsExactlyTheApprovedTools()
    {
        await using var server = await FinanceMcpHttpServer.CreateAsync(postgres.ConnectionString);

        var tools = (await server.McpClient.ListToolsAsync(cancellationToken: CancellationToken.None))
            .Select(tool => tool.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedTools, tools);
    }

    [Fact]
    public async Task EveryToolExecutesOverHttpAgainstPostgreSqlWithoutChangingData()
    {
        await using var server = await FinanceMcpHttpServer.CreateAsync(postgres.ConnectionString);
        await using var dbContext = postgres.CreateDbContext();
        var directTools = new FinanceMcpTools(dbContext);
        var cancellationToken = CancellationToken.None;
        var before = await postgres.GetCountsAsync();

        var summary = await CallAsync<FinanceMcpResult<McpSalesSummary>>(
            server.McpClient,
            "get_sales_summary",
            new Dictionary<string, object?> { ["startDate"] = "2026-07-13", ["endDate"] = "2026-07-15" },
            cancellationToken);
        var comparison = await CallAsync<FinanceMcpResult<McpPeriodComparison>>(
            server.McpClient,
            "compare_sales_periods",
            new Dictionary<string, object?>
            {
                ["currentStartDate"] = "2026-07-13",
                ["currentEndDate"] = "2026-07-15",
                ["previousStartDate"] = "2026-07-06",
                ["previousEndDate"] = "2026-07-12"
            },
            cancellationToken);
        var topProducts = await CallAsync<FinanceMcpResult<McpTopProducts>>(
            server.McpClient,
            "get_top_products",
            new Dictionary<string, object?> { ["startDate"] = "2026-07-01", ["endDate"] = "2026-07-15", ["limit"] = 5 },
            cancellationToken);
        var historical = await CallAsync<FinanceMcpResult<McpHistoricalSales>>(
            server.McpClient,
            "get_historical_sales",
            new Dictionary<string, object?> { ["startYear"] = 2021, ["endYear"] = 2025 },
            cancellationToken);
        var budget = await CallAsync<FinanceMcpResult<McpBudgetTarget>>(
            server.McpClient,
            "get_budget_target",
            new Dictionary<string, object?> { ["year"] = 2026, ["month"] = null },
            cancellationToken);

        Assert.Equivalent(
            await directTools.GetSalesSummaryAsync("2026-07-13", "2026-07-15", cancellationToken),
            summary,
            strict: true);
        Assert.Equivalent(
            await directTools.CompareSalesPeriodsAsync("2026-07-13", "2026-07-15", "2026-07-06", "2026-07-12", cancellationToken),
            comparison,
            strict: true);
        Assert.Equivalent(
            await directTools.GetTopProductsAsync("2026-07-01", "2026-07-15", 5, cancellationToken),
            topProducts,
            strict: true);
        Assert.Equivalent(
            await directTools.GetHistoricalSalesAsync(2021, 2025, cancellationToken),
            historical,
            strict: true);
        Assert.Equivalent(
            await directTools.GetBudgetTargetAsync(2026, null, cancellationToken),
            budget,
            strict: true);
        Assert.Equal(before, await postgres.GetCountsAsync());
    }

    [Theory]
    [InlineData("not-a-date", "2026-07-15")]
    [InlineData("2024-01-01", "2026-01-01")]
    public async Task HttpToolCallsPreserveValidationAndTypedFailures(string startDate, string endDate)
    {
        await using var server = await FinanceMcpHttpServer.CreateAsync(postgres.ConnectionString);

        var result = await CallAsync<FinanceMcpResult<McpSalesSummary>>(
            server.McpClient,
            "get_sales_summary",
            new Dictionary<string, object?> { ["startDate"] = startDate, ["endDate"] = endDate },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task CallerCancellationIsPropagated()
    {
        await using var server = await FinanceMcpHttpServer.CreateAsync(postgres.ConnectionString);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await server.McpClient.CallToolAsync(
            "get_sales_summary",
            new Dictionary<string, object?> { ["startDate"] = "2026-07-13", ["endDate"] = "2026-07-15" },
            cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task LiveAndReadyHealthReportHealthyForCurrentSchema()
    {
        await using var server = await FinanceMcpHttpServer.CreateAsync(postgres.ConnectionString);

        var live = await server.HttpClient.GetAsync("/health/live", CancellationToken.None);
        var ready = await server.HttpClient.GetAsync("/health/ready", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task ReadinessIsUnhealthyWithoutPostgreSqlWhileLivenessRemainsHealthy()
    {
        const string unavailableDatabase = "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1;Command Timeout=1";
        await using var server = await FinanceMcpHttpServer.CreateWithoutMcpClient(unavailableDatabase);

        var live = await server.HttpClient.GetAsync("/health/live", CancellationToken.None);
        var ready = await server.HttpClient.GetAsync("/health/ready", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.DoesNotContain("Password", await ready.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IndependentKestrelHostUsesEnvironmentConfiguredUrl()
    {
        var port = GetAvailablePort();
        var assemblyPath = Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "CfoAgent.FinanceMcpServer",
            "bin",
            GetBuildConfiguration(),
            "net10.0",
            "CfoAgent.FinanceMcpServer.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.Environment[FinanceDatabaseConfiguration.EnvironmentVariableName] = postgres.ConnectionString;
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Testing";

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The Finance MCP HTTP host could not be started.");
        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            using var timeout = new CancellationTokenSource();
            timeout.CancelAfter(TimeSpan.FromSeconds(15));

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

    private static async Task<T> CallAsync<T>(
        McpClient client,
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var call = await client.CallToolAsync(toolName, arguments, cancellationToken: cancellationToken);
        var content = Assert.IsType<TextContentBlock>(Assert.Single(call.Content));
        return JsonSerializer.Deserialize<T>(content.Text, JsonOptions)
            ?? throw new JsonException($"Tool {toolName} returned an empty result.");
    }

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

    private sealed class FinanceMcpHttpServer : IAsyncDisposable
    {
        private readonly WebApplicationFactory<FinanceMcpProgram> factory;

        private FinanceMcpHttpServer(
            WebApplicationFactory<FinanceMcpProgram> factory,
            HttpClient httpClient,
            McpClient? mcpClient)
        {
            this.factory = factory;
            HttpClient = httpClient;
            McpClient = mcpClient!;
        }

        public HttpClient HttpClient { get; }

        public McpClient McpClient { get; }

        public static async Task<FinanceMcpHttpServer> CreateAsync(string connectionString)
        {
            var server = await CreateWithoutMcpClient(connectionString);
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
            return new FinanceMcpHttpServer(server.factory, server.HttpClient, mcpClient);
        }

        public static Task<FinanceMcpHttpServer> CreateWithoutMcpClient(string connectionString)
        {
            var factory = new FinanceMcpWebApplicationFactory(connectionString);
            var httpClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://finance-mcp.test")
            });
            return Task.FromResult(new FinanceMcpHttpServer(factory, httpClient, null));
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

    private sealed class FinanceMcpWebApplicationFactory(string connectionString) : WebApplicationFactory<FinanceMcpProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:FinanceDatabase", connectionString);
        }
    }
}
