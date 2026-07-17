using System.Security.Cryptography;
using System.Text.Json;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Data;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Tests.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class FinanceMcpProcessTests
{
    private static readonly DateOnly DemoDate = new(2026, 7, 15);
    private static readonly string[] ExpectedTools =
    [
        "compare_sales_periods",
        "get_budget_target",
        "get_historical_sales",
        "get_sales_summary",
        "get_top_products"
    ];

    [Fact]
    public async Task DiscoversExactlyTheApprovedReadOnlyFinanceTools()
    {
        await using var fixture = FinanceFixture.Create();

        var tools = await fixture.Client.DiscoverToolsAsync(CancellationToken.None);

        Assert.Equal(ExpectedTools, tools);
    }

    [Fact]
    public async Task InvokesEveryFinanceToolWithoutChangingTheDatabase()
    {
        await using var fixture = FinanceFixture.Create();
        var before = await GetDatabaseHashAsync();

        var summary = await fixture.Client.GetCurrentWeekSummaryAsync(CancellationToken.None);
        var comparison = await fixture.Client.GetWeekOverWeekComparisonAsync(CancellationToken.None);
        var topProducts = await fixture.Client.GetCurrentMonthTopProductsAsync(CancellationToken.None);
        var historical = await fixture.Client.GetHistoricalYearlyTotalsAsync(CancellationToken.None);
        var budget = await fixture.Client.GetBudgetTargetAsync(DemoDate.Year, null, CancellationToken.None);

        var after = await GetDatabaseHashAsync();

        Assert.Equal(new SalesPeriod(new DateOnly(2026, 7, 13), DemoDate), summary.Period);
        Assert.Equal(summary.Period, comparison.CurrentWeek.Period);
        Assert.Equal(new SalesPeriod(new DateOnly(2026, 7, 1), DemoDate), topProducts.Period);
        Assert.NotEmpty(historical.Totals);
        Assert.True(budget.IsAvailable);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task FinanceMcpAndLocalServicesAgreeForDemoData()
    {
        await using var fixture = FinanceFixture.Create();

        var mcpSummary = await fixture.Client.GetCurrentWeekSummaryAsync(CancellationToken.None);
        var mcpComparison = await fixture.Client.GetWeekOverWeekComparisonAsync(CancellationToken.None);
        var mcpProducts = await fixture.Client.GetCurrentMonthTopProductsAsync(CancellationToken.None);
        var mcpHistorical = await fixture.Client.GetHistoricalYearlyTotalsAsync(CancellationToken.None);
        var mcpBudget = await fixture.Client.GetBudgetTargetAsync(DemoDate.Year, null, CancellationToken.None);

        Assert.Equal(JsonSerializer.Serialize(await fixture.LocalService.GetCurrentWeekSummaryAsync(CancellationToken.None)), JsonSerializer.Serialize(mcpSummary));
        Assert.Equal(JsonSerializer.Serialize(await fixture.LocalService.GetWeekOverWeekComparisonAsync(CancellationToken.None)), JsonSerializer.Serialize(mcpComparison));
        Assert.Equal(JsonSerializer.Serialize(await fixture.LocalService.GetCurrentMonthTopProductsAsync(CancellationToken.None)), JsonSerializer.Serialize(mcpProducts));
        Assert.Equal(JsonSerializer.Serialize(await fixture.LocalService.GetHistoricalYearlyTotalsAsync(CancellationToken.None)), JsonSerializer.Serialize(mcpHistorical));
        Assert.Equal(JsonSerializer.Serialize(await fixture.LocalService.GetBudgetTargetAsync(DemoDate.Year, null, CancellationToken.None)), JsonSerializer.Serialize(mcpBudget));
    }

    [Theory]
    [InlineData("not-a-date", "2026-07-15")]
    [InlineData("2024-01-01", "2026-01-01")]
    public async Task RejectsInvalidOrExcessiveSalesSummaryDateRanges(string startDate, string endDate)
    {
        await using var connection = await RawFinanceConnection.CreateAsync();

        var result = await connection.Client.CallToolAsync(
            "get_sales_summary",
            new Dictionary<string, object?>
            {
                ["startDate"] = startDate,
                ["endDate"] = endDate
            },
            cancellationToken: CancellationToken.None);

        var content = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("\"isSuccess\":false", content.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServerFailsToInitializeWhenTheDatabaseIsMissing()
    {
        await using var fixture = await MissingDatabaseConnection.CreateAsync();

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.ConnectAsync());
    }

    [Fact]
    public async Task CallerCancellationIsPropagatedWithoutStartingFallback()
    {
        await using var fixture = FinanceFixture.Create();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Client.GetCurrentWeekSummaryAsync(cancellation.Token));
    }

    [Fact]
    public async Task StoppedFinanceMcpUsesTheDeterministicLocalFallback()
    {
        await using var fixture = FinanceFixture.Create();
        await fixture.Client.GetCurrentWeekSummaryAsync(CancellationToken.None);
        await fixture.StopClientAsync();
        var fallback = new FinanceMcpFallback(
            Options.Create(FinanceFixture.CreateOptions()),
            NullLogger<FinanceMcpFallback>.Instance);

        var result = await fallback.ExecuteAsync(
            fixture.Client.GetCurrentWeekSummaryAsync,
            fixture.LocalService.GetCurrentWeekSummaryAsync,
            CancellationToken.None);

        Assert.True(result.UsedFallback);
        Assert.Equal("unavailable", result.FallbackReason);
        Assert.Equal(
            JsonSerializer.Serialize(await fixture.LocalService.GetCurrentWeekSummaryAsync(CancellationToken.None)),
            JsonSerializer.Serialize(result.Value));
    }

    private static async Task<string> GetDatabaseHashAsync()
    {
        var bytes = await File.ReadAllBytesAsync(GetDatabasePath());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string GetDatabasePath() => Path.Combine(FindRepositoryRoot(), "data", "cfo-agent.db");

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CfoAgent.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root could not be located for the Finance MCP process test.");
    }

    private sealed class FinanceFixture : IAsyncDisposable
    {
        private readonly FinanceDbContext dbContext;
        private bool clientStopped;

        private FinanceFixture(FinanceMcpClient client, SalesAnalysisService localService, FinanceDbContext dbContext)
        {
            Client = client;
            LocalService = localService;
            this.dbContext = dbContext;
        }

        public FinanceMcpClient Client { get; }

        public SalesAnalysisService LocalService { get; }

        public static FinanceFixture Create()
        {
            var options = Options.Create(CreateOptions());
            var clock = new FixedTimeProvider(DemoDate);
            var client = new FinanceMcpClient(
                options,
                new TestHostEnvironment(FindRepositoryRoot()),
                clock,
                NullLogger<FinanceMcpClient>.Instance);
            var dbContext = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
                .UseSqlite($"Data Source={GetDatabasePath()};Mode=ReadOnly")
                .Options);
            return new FinanceFixture(client, new SalesAnalysisService(dbContext, clock), dbContext);
        }

        public static McpOptions CreateOptions() => new()
        {
            UseLocalFallback = true,
            Finance = new FinanceMcpOptions
            {
                Enabled = true,
                ServerProjectPath = "tools/CfoAgent.FinanceMcpServer",
                TimeoutSeconds = 15
            },
            KnowledgeFiles = new KnowledgeFileMcpOptions
            {
                Enabled = false,
                RootPath = "data/knowledge",
                TimeoutSeconds = 15
            }
        };

        public async Task StopClientAsync()
        {
            await Client.DisposeAsync();
            clientStopped = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!clientStopped)
            {
                await Client.DisposeAsync();
            }

            await dbContext.DisposeAsync();
        }
    }

    private sealed class RawFinanceConnection(McpClient client) : IAsyncDisposable
    {
        public McpClient Client { get; } = client;

        public static async Task<RawFinanceConnection> CreateAsync()
        {
            var projectPath = Path.Combine(FindRepositoryRoot(), "tools", "CfoAgent.FinanceMcpServer");
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = "dotnet",
                Arguments = ["run", "--project", projectPath, "--no-build", "--configuration", GetBuildConfiguration()]
            });
            return new RawFinanceConnection(await McpClient.CreateAsync(transport));
        }

        public ValueTask DisposeAsync() => Client.DisposeAsync();
    }

    private sealed class MissingDatabaseConnection : IAsyncDisposable
    {
        private readonly string root;

        private MissingDatabaseConnection(string root)
        {
            this.root = root;
        }

        public static Task<MissingDatabaseConnection> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), $"cfo-finance-mcp-missing-db-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var buildDirectory = Path.Combine(
                FindRepositoryRoot(),
                "tools",
                "CfoAgent.FinanceMcpServer",
                "bin",
                GetBuildConfiguration(),
                "net10.0");
            foreach (var file in Directory.EnumerateFiles(buildDirectory))
            {
                File.Copy(file, Path.Combine(root, Path.GetFileName(file)));
            }

            return Task.FromResult(new MissingDatabaseConnection(root));
        }

        public async Task ConnectAsync()
        {
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = "dotnet",
                Arguments = [Path.Combine(root, "CfoAgent.FinanceMcpServer.dll")],
                WorkingDirectory = root
            });
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
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

    private static string GetBuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "CfoAgent.Api.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
