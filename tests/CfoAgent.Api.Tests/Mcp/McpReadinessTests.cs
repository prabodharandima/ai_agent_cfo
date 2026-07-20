using CfoAgent.Api.Configuration;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Health;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class McpReadinessTests
{
    [Fact]
    public async Task ReadyWhenRequiredFinanceCapabilitiesAreAvailable()
    {
        var finance = new StubFinanceRemoteClient();
        var check = CreateCheck(finance, financeEnabled: true);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(1, finance.DiscoveryCalls);
    }

    [Fact]
    public async Task UnhealthyWhenFinanceDependencyIsUnavailableWithoutLeakingDetails()
    {
        var finance = new StubFinanceRemoteClient
        {
            Discover = _ => throw new McpDependencyException(
                "Finance MCP",
                McpDependencyFailureKind.Unavailable,
                new HttpRequestException("http://finance-mcp.internal/private"))
        };
        var check = CreateCheck(finance, financeEnabled: true);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Finance MCP is unavailable.", result.Description);
        Assert.DoesNotContain("finance-mcp.internal", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnhealthyWhenFinanceRequiredCapabilityIsMissing()
    {
        var finance = new StubFinanceRemoteClient
        {
            Discover = _ => throw new McpDependencyException(
                "Finance MCP",
                McpDependencyFailureKind.CapabilityMismatch)
        };
        var check = CreateCheck(finance, financeEnabled: true);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Finance MCP is unavailable.", result.Description);
    }

    [Fact]
    public async Task ReadinessCallerCancellationPropagates()
    {
        var finance = new StubFinanceRemoteClient
        {
            Discover = token =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            }
        };
        var check = CreateCheck(finance, financeEnabled: true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            check.CheckHealthAsync(new HealthCheckContext(), cancellation.Token));
    }

    private static McpConfigurationHealthCheck CreateCheck(StubFinanceRemoteClient finance, bool financeEnabled) => new(
        Options.Create(new McpOptions
        {
            Finance = new FinanceMcpOptions
            {
                Enabled = financeEnabled,
                BaseUrl = "http://finance-mcp.test",
                TimeoutSeconds = 5
            },
            KnowledgeFiles = new KnowledgeFileMcpOptions
            {
                Enabled = false,
                BaseUrl = "http://knowledge-mcp.test",
                RootPath = "unused",
                TimeoutSeconds = 5
            }
        }),
        finance,
        new StubKnowledgeRemoteClient(),
        new StubKnowledgeClient());

    private sealed class StubFinanceRemoteClient : IFinanceMcpRemoteClient
    {
        public Func<CancellationToken, Task<IReadOnlyList<string>>> Discover { get; init; } =
            _ => Task.FromResult<IReadOnlyList<string>>(
                ["compare_sales_periods", "get_budget_target", "get_historical_sales", "get_sales_summary", "get_top_products"]);

        public int DiscoveryCalls { get; private set; }

        public Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken)
        {
            DiscoveryCalls++;
            return Discover(cancellationToken);
        }

        public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubKnowledgeClient : IKnowledgeFileMcpClient
    {
        public Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubKnowledgeRemoteClient : IKnowledgeFileMcpRemoteClient
    {
        public Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
