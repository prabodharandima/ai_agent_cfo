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
    [InlineData("Are there any sales happen yesterday?", 2026, 7, 14, "Yesterday")]
    [InlineData("Give me the sales summary of today.", 2026, 7, 15, "Today")]
    [InlineData("Give me the sales summary of 10th of June this year?", 2026, 6, 10, "Selected date")]
    public async Task SalesSummaryUsesTheRequestedSingleDate(string message, int year, int month, int day, string expectedLabel)
    {
        using var chatClient = TestChatClient.CreateMvp();
        var financeClient = new CapturingFinanceClient();
        var agent = new SalesAnalysisAgent(
            chatClient,
            financeClient,
            new FixedTimeProvider(new DateOnly(2026, 7, 15)));

        var result = await agent.GetWeeklySummaryAsync(new AgentRequest(message), CancellationToken.None);

        var expectedDate = new DateOnly(year, month, day);
        Assert.Equal(new SalesPeriod(expectedDate, expectedDate), financeClient.RequestedPeriod);
        Assert.Equal(expectedLabel, result.DataPeriod?.Label);
        Assert.Equal(expectedDate, result.DataPeriod?.From);
        Assert.Equal(expectedDate, result.DataPeriod?.To);
    }

    [Fact]
    public async Task SalesSummaryWithoutAnExplicitDateUsesTheCurrentWeek()
    {
        using var chatClient = TestChatClient.CreateMvp();
        var financeClient = new CapturingFinanceClient();
        var agent = new SalesAnalysisAgent(
            chatClient,
            financeClient,
            new FixedTimeProvider(new DateOnly(2026, 7, 15)));

        var result = await agent.GetWeeklySummaryAsync(
            new AgentRequest("Give me the sales summary of this week."),
            CancellationToken.None);

        var expectedPeriod = new SalesPeriod(new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 15));
        Assert.Equal(expectedPeriod, financeClient.RequestedPeriod);
        Assert.Equal("Current week", result.DataPeriod?.Label);
    }

    private sealed class CapturingFinanceClient : IFinanceMcpClient
    {
        public SalesPeriod? RequestedPeriod { get; private set; }

        public Task<SalesSummary> GetSalesSummaryAsync(SalesPeriod period, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedPeriod = period;
            return Task.FromResult(new SalesSummary(
                period,
                100m,
                40m,
                60m,
                60m,
                1m,
                1,
                100m,
                null,
                Array.Empty<string>()));
        }

        public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
