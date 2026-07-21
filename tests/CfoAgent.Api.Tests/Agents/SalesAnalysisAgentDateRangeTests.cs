using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Tests.AI;
using CfoAgent.Api.Tests.Finance;

namespace CfoAgent.Api.Tests.Agents;

public sealed class SalesAnalysisAgentDateRangeTests
{
    [Theory]
    [InlineData("Give me the sales summary since yesterday.", "{\"startDate\":\"2026-07-14\",\"endDate\":\"2026-07-15\"}", 2026, 7, 14, 2026, 7, 15)]
    [InlineData("Give me the sales summary of last week.", "{\"startDate\":\"2026-07-06\",\"endDate\":\"2026-07-12\"}", 2026, 7, 6, 2026, 7, 12)]
    [InlineData("Give me the sales summary of 10th of June this year.", "{\"startDate\":\"2026-06-10\",\"endDate\":\"2026-06-10\"}", 2026, 6, 10, 2026, 6, 10)]
    public async Task SalesSummaryUsesTheLlmResolvedAndValidatedDateRange(
        string message,
        string modelResponse,
        int startYear,
        int startMonth,
        int startDay,
        int endYear,
        int endMonth,
        int endDay)
    {
        using var chatClient = CreateDateRangeClient(modelResponse);
        var financeClient = new CapturingFinanceClient();
        var agent = new SalesAnalysisAgent(
            chatClient,
            financeClient,
            new FixedTimeProvider(new DateOnly(2026, 7, 15)));

        var result = await agent.GetWeeklySummaryAsync(new AgentRequest(message), CancellationToken.None);

        var expectedPeriod = new SalesPeriod(
            new DateOnly(startYear, startMonth, startDay),
            new DateOnly(endYear, endMonth, endDay));
        Assert.Equal(expectedPeriod, financeClient.RequestedPeriod);
        Assert.Equal(expectedPeriod.StartDate, result.DataPeriod?.From);
        Assert.Equal(expectedPeriod.EndDate, result.DataPeriod?.To);
        Assert.Equal("Requested period", result.DataPeriod?.Label);
    }

    [Theory]
    [InlineData("{\"startDate\":\"2026-07-16\",\"endDate\":\"2026-07-16\"}")]
    [InlineData("{\"startDate\":\"2026-07-15\",\"endDate\":\"2026-07-14\"}")]
    [InlineData("{\"startDate\":\"15/07/2026\",\"endDate\":\"15/07/2026\"}")]
    public async Task InvalidLlmDateRangeIsRejectedBeforeFinanceMcpIsCalled(string modelResponse)
    {
        using var chatClient = CreateDateRangeClient(modelResponse);
        var financeClient = new CapturingFinanceClient();
        var agent = new SalesAnalysisAgent(
            chatClient,
            financeClient,
            new FixedTimeProvider(new DateOnly(2026, 7, 15)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.GetWeeklySummaryAsync(new AgentRequest("Give me the sales summary."), CancellationToken.None));

        Assert.Null(financeClient.RequestedPeriod);
    }

    private static TestChatClient CreateDateRangeClient(string modelResponse) => new(
        (prompt, _, _) => Task.FromResult(
            prompt.Contains("SALES_SUMMARY_PERIOD_REQUEST:", StringComparison.Ordinal)
                ? modelResponse
                : "Verified test response."));

    private sealed class CapturingFinanceClient : IFinanceMcpClient
    {
        public SalesPeriod? RequestedPeriod { get; private set; }

        public Task<SalesSummary> GetSalesSummaryAsync(SalesPeriod period, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedPeriod = period;
            return Task.FromResult(new SalesSummary(period, 100m, 40m, 60m, 60m, 1m, 1, 100m, null, Array.Empty<string>()));
        }

        public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
