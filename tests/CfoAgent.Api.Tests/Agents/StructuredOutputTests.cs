using CfoAgent.Api.AI;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Tests.AI;
using CfoAgent.Api.Tests.Finance;
using Microsoft.Extensions.AI;

namespace CfoAgent.Api.Tests.Agents;

public sealed class StructuredOutputTests
{
    private static readonly DateOnly CurrentDate = new(2026, 7, 15);

    [Fact]
    public async Task ClassifyAsync_UsesStructuredIntentOutput()
    {
        var responseFormatSeen = false;
        using var client = new TestChatClient((_, options, _) =>
        {
            responseFormatSeen = options?.ResponseFormat is ChatResponseFormatJson;
            return Task.FromResult("{\"intent\":\"Knowledge\"}");
        });
        var orchestrator = new CfoOrchestratorAgent(null!, null!, null!, new AgentResultComposer(), client);

        var intent = await orchestrator.ClassifyAsync("What documented risks should leadership review?");

        Assert.Equal(CfoIntent.Knowledge, intent);
        Assert.True(responseFormatSeen);
    }

    [Fact]
    public async Task ClassifyAsync_MalformedStructuredIntentUsesDeterministicFallback()
    {
        using var client = new TestChatClient((_, _, _) => Task.FromResult("{not-json}"));
        var orchestrator = new CfoOrchestratorAgent(null!, null!, null!, new AgentResultComposer(), client);

        var intent = await orchestrator.ClassifyAsync("Show the top products this month.");

        Assert.Equal(CfoIntent.TopProducts, intent);
    }

    [Fact]
    public async Task SalesSummary_UsesValidatedStructuredDateRangeBeforeCallingFinanceMcp()
    {
        var responseFormatSeen = false;
        using var client = new TestChatClient((prompt, options, _) =>
        {
            if (prompt.Contains("STRUCTURED_SALES_PERIOD_OUTPUT", StringComparison.Ordinal))
            {
                responseFormatSeen = options?.ResponseFormat is ChatResponseFormatJson;
                return Task.FromResult("{\"startDate\":\"2026-07-01\",\"endDate\":\"2026-07-15\"}");
            }

            return Task.FromResult("Verified presentation.");
        });
        var finance = new RecordingFinanceMcpClient();
        var agent = new SalesAnalysisAgent(client, finance, new FixedTimeProvider(CurrentDate));

        var result = await agent.GetWeeklySummaryAsync(new AgentRequest("Give me the sales summary for July."), CancellationToken.None);

        Assert.True(responseFormatSeen);
        Assert.Equal(new SalesPeriod(new DateOnly(2026, 7, 1), CurrentDate), finance.LastRequestedPeriod);
        Assert.NotNull(result.DataPeriod);
        Assert.Equal(new DateOnly(2026, 7, 1), result.DataPeriod.From);
        Assert.Equal(CurrentDate, result.DataPeriod.To);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"startDate\":\"2026-07-01\",\"endDate\":\"not-a-date\"}")]
    [InlineData("{\"startDate\":\"2026-07-01\",\"endDate\":\"2026-07-16\"}")]
    [InlineData("{\"startDate\":\"2026-07-15\",\"endDate\":\"2026-07-01\"}")]
    [InlineData("{\"startDate\":\"2025-07-14\",\"endDate\":\"2026-07-15\"}")]
    public async Task SalesSummary_InvalidStructuredDateRangeIsRejectedBeforeCallingFinanceMcp(string response)
    {
        using var client = new TestChatClient((_, _, _) => Task.FromResult(response));
        var finance = new RecordingFinanceMcpClient();
        var agent = new SalesAnalysisAgent(client, finance, new FixedTimeProvider(CurrentDate));

        var exception = await Assert.ThrowsAsync<AiProviderException>(() =>
            agent.GetWeeklySummaryAsync(new AgentRequest("Give me the sales summary for the requested period."), CancellationToken.None));

        Assert.Equal(AiProviderFailureKind.InvalidResponse, exception.FailureKind);
        Assert.Equal(0, finance.SummaryCalls);
    }

    [Fact]
    public async Task SalesSummary_PropagatesCancellationBeforeCallingFinanceMcp()
    {
        using var client = new TestChatClient(async (_, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return "{\"startDate\":\"2026-07-13\",\"endDate\":\"2026-07-15\"}";
        });
        var finance = new RecordingFinanceMcpClient();
        var agent = new SalesAnalysisAgent(client, finance, new FixedTimeProvider(CurrentDate));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            agent.GetWeeklySummaryAsync(new AgentRequest("Give me the sales summary for yesterday."), cancellation.Token));

        Assert.Equal(0, finance.SummaryCalls);
    }

    private sealed class RecordingFinanceMcpClient : IFinanceMcpClient
    {
        public int SummaryCalls { get; private set; }

        public SalesPeriod? LastRequestedPeriod { get; private set; }

        public Task<SalesSummary> GetSalesSummaryAsync(SalesPeriod period, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SummaryCalls++;
            LastRequestedPeriod = period;
            return Task.FromResult(new SalesSummary(
                period,
                200m,
                80m,
                120m,
                60m,
                2m,
                1,
                200m,
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
