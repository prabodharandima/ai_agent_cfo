using System.Text.Json;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Tests.Finance;
using Microsoft.Extensions.Logging.Abstractions;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class FinanceMcpClientTests
{
    [Fact]
    public async Task TypedOperationsInvokeExpectedToolsWithCanonicalArguments()
    {
        var adapter = new RecordingToolAdapter();
        var client = new FinanceMcpClient(
            adapter,
            new FixedTimeProvider(new DateOnly(2026, 7, 15)),
            NullLogger<FinanceMcpClient>.Instance);

        await client.GetCurrentWeekSummaryAsync(CancellationToken.None);
        await client.GetWeekOverWeekComparisonAsync(CancellationToken.None);
        await client.GetCurrentMonthTopProductsAsync(CancellationToken.None);
        await client.GetHistoricalYearlyTotalsAsync(CancellationToken.None);
        await client.GetBudgetTargetAsync(2026, null, CancellationToken.None);

        Assert.Collection(
            adapter.Calls,
            call => AssertCall(call, "get_sales_summary", ("startDate", "2026-07-13"), ("endDate", "2026-07-15")),
            call => AssertCall(
                call,
                "compare_sales_periods",
                ("currentStartDate", "2026-07-13"),
                ("currentEndDate", "2026-07-15"),
                ("previousStartDate", "2026-07-06"),
                ("previousEndDate", "2026-07-12")),
            call => AssertCall(call, "get_top_products", ("startDate", "2026-07-01"), ("endDate", "2026-07-15"), ("limit", 5)),
            call => AssertCall(call, "get_historical_sales", ("startYear", 2021), ("endYear", 2025)),
            call => AssertCall(call, "get_budget_target", ("year", 2026), ("month", null)));
    }

    [Fact]
    public async Task CurrentWeekSummaryUsesTheInjectedCurrentDateWhenTheWeekStartsToday()
    {
        var adapter = new RecordingToolAdapter();
        var client = new FinanceMcpClient(
            adapter,
            new FixedTimeProvider(new DateOnly(2026, 7, 20)),
            NullLogger<FinanceMcpClient>.Instance);

        await client.GetCurrentWeekSummaryAsync(CancellationToken.None);

        AssertCall(adapter.Calls.Single(), "get_sales_summary", ("startDate", "2026-07-20"), ("endDate", "2026-07-20"));
    }

    [Fact]
    public async Task SalesSummaryForAnExplicitPeriodUsesCanonicalDateArguments()
    {
        var adapter = new RecordingToolAdapter();
        var client = new FinanceMcpClient(
            adapter,
            new FixedTimeProvider(new DateOnly(2026, 7, 15)),
            NullLogger<FinanceMcpClient>.Instance);

        await client.GetSalesSummaryAsync(
            new SalesPeriod(new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 10)),
            CancellationToken.None);

        AssertCall(adapter.Calls.Single(), "get_sales_summary", ("startDate", "2026-06-10"), ("endDate", "2026-06-10"));
    }

    private static void AssertCall(
        RecordedCall call,
        string expectedToolName,
        params (string Name, object? Value)[] expectedArguments)
    {
        Assert.Equal(expectedToolName, call.ToolName);
        Assert.Equal(expectedArguments.Length, call.Arguments.Count);
        foreach (var expected in expectedArguments)
        {
            Assert.True(call.Arguments.TryGetValue(expected.Name, out var actual));
            Assert.Equal(expected.Value, actual);
        }
    }

    private sealed class RecordingToolAdapter : IMcpToolAdapter
    {
        public List<RecordedCall> Calls { get; } = [];

        public Task<IReadOnlyList<string>> GetApprovedToolNamesAsync(
            IEnumerable<string>? requiredToolNames,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(requiredToolNames?.ToArray() ?? []);

        public Task<JsonElement> CallApprovedToolAsync(
            string toolName,
            IReadOnlyDictionary<string, object?>? arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(new RecordedCall(
                toolName,
                arguments is null
                    ? new Dictionary<string, object?>()
                    : new Dictionary<string, object?>(arguments, StringComparer.Ordinal)));
            return Task.FromResult(JsonSerializer.SerializeToElement(ResponseFor(toolName)));
        }

        private static object ResponseFor(string toolName) => toolName switch
        {
            "get_sales_summary" => Summary(),
            "compare_sales_periods" => new
            {
                currentPeriod = Summary(),
                previousPeriod = Summary(),
                netRevenueChange = 0m,
                netRevenueChangePercentage = 0m,
                direction = "unchanged",
                warnings = Array.Empty<string>()
            },
            "get_top_products" => new
            {
                period = Period(),
                products = Array.Empty<object>(),
                warnings = Array.Empty<string>()
            },
            "get_historical_sales" => new
            {
                totals = new[] { new { year = 2025, netRevenue = 100m } },
                warnings = Array.Empty<string>()
            },
            "get_budget_target" => new
            {
                year = 2026,
                month = (int?)null,
                isAvailable = true,
                salesTarget = 100m,
                profitTarget = 20m,
                assumptionReference = "budget",
                warnings = Array.Empty<string>()
            },
            _ => throw new InvalidOperationException("Unexpected test tool.")
        };

        private static object Summary() => new
        {
            period = Period(),
            netRevenue = 100m,
            costOfGoodsSold = 40m,
            grossProfit = 60m,
            grossMarginPercent = 60m,
            quantitySold = 1m,
            orderCount = 1,
            averageOrderValue = 100m,
            topProduct = (object?)null,
            warnings = Array.Empty<string>()
        };

        private static object Period() => new { startDate = "2026-07-13", endDate = "2026-07-15" };
    }

    private sealed record RecordedCall(string ToolName, IReadOnlyDictionary<string, object?> Arguments);
}
