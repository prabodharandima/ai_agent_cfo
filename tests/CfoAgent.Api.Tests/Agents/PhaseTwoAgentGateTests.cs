using System.Text.Json;
using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Tests.Finance;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Agents;

public sealed class PhaseTwoAgentGateTests
{
    private static readonly DateOnly DemoDate = new(2026, 7, 15);
    [Fact]
    public async Task SalesAgentUsesMcpResultsAndIncludesTheVerifiedPayloadInItsAnswer()
    {
        using var client = CreateClient();
        var agent = CreateSalesAgent(client, new FakeFinanceMcpClient());
        var request = new AgentRequest("Give me this week's sales summary.");

        var summary = await agent.GetWeeklySummaryAsync(request, CancellationToken.None);
        var comparison = await agent.GetWeekOverWeekComparisonAsync(request, CancellationToken.None);
        var topProducts = await agent.GetCurrentMonthTopProductsAsync(request, CancellationToken.None);

        Assert.Equal(AgentResponseType.SalesSummary, summary.ResponseType);
        Assert.Equal(AgentDefinitions.SalesAnalysis.Name, Assert.Single(summary.AgentNames));
        AssertPayloadIsInAnswer(summary);
        AssertPayloadIsInAnswer(comparison);
        AssertPayloadIsInAnswer(topProducts);
        Assert.Equal(5, Assert.IsType<TopProductsResult>(topProducts.StructuredData).Products.Count);
        Assert.True(Assert.IsType<WeeklySalesComparison>(comparison.StructuredData).NetRevenueChange > 0m);
    }

    [Fact]
    public async Task ForecastAgentReturnsVerifiedMcpHistoricalInputsAndDeterministicPayload()
    {
        using var client = CreateClient();
        var agent = CreateForecastingAgent(client, new FakeFinanceMcpClient());

        var result = await agent.GetForecastAsync(new AgentRequest("Give me the sales forecast."), CancellationToken.None);

        var forecast = Assert.IsType<SalesForecastResult>(result.StructuredData);
        Assert.Equal(AgentResponseType.Forecast, result.ResponseType);
        Assert.Equal(AgentDefinitions.Forecasting.Name, Assert.Single(result.AgentNames));
        Assert.Equal(5, forecast.Forecasts.Count);
        Assert.Equal(forecast.Assumptions, result.Assumptions);
        Assert.Equal(forecast.Warnings, result.Warnings);
        AssertPayloadIsInAnswer(result);
    }

    [Fact]
    public async Task ForecastAgentReturnsInsufficientDataWarningsWithoutInventingValues()
    {
        using var client = CreateClient();
        var mcp = new FakeFinanceMcpClient { Historical = new HistoricalYearlySalesResult([], Array.Empty<string>()) };
        var agent = CreateForecastingAgent(client, mcp);

        var result = await agent.GetForecastAsync(new AgentRequest("Give me the sales forecast."), CancellationToken.None);

        var forecast = Assert.IsType<SalesForecastResult>(result.StructuredData);
        Assert.Empty(forecast.Forecasts);
        Assert.NotEmpty(result.Warnings);
        AssertPayloadIsInAnswer(result);
    }

    [Fact]
    public async Task SalesAgentPropagatesCancellationFromTheMockClient()
    {
        using var client = CreateClient(simulatedDelayMilliseconds: 5_000);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(TimeSpan.FromMilliseconds(25));
        var agent = CreateSalesAgent(client, new FakeFinanceMcpClient());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            agent.GetWeeklySummaryAsync(new AgentRequest("Give me this week's sales summary."), cancellationSource.Token));
    }

    [Fact]
    public async Task ForecastAgentWrapsSimulatedMockFailureAsAControlledAgentError()
    {
        using var client = CreateClient(simulateFailure: true);
        var agent = CreateForecastingAgent(client, new FakeFinanceMcpClient());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.GetForecastAsync(new AgentRequest("Give me the sales forecast."), CancellationToken.None));

        Assert.Equal("The forecasting agent could not produce a forecast.", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private static SalesAnalysisAgent CreateSalesAgent(MockChatClient client, IFinanceMcpClient mcp) =>
        new(client, mcp);

    private static ForecastingAgent CreateForecastingAgent(MockChatClient client, IFinanceMcpClient mcp) =>
        new(new SalesForecastingService(), client, mcp);

    private static MockChatClient CreateClient(int simulatedDelayMilliseconds = 0, bool simulateFailure = false) => new(Options.Create(new AiOptions
    {
        Provider = "Mock",
        Model = "DeterministicMock",
        SimulatedDelayMilliseconds = simulatedDelayMilliseconds,
        SimulateFailure = simulateFailure
    }));

    private static void AssertPayloadIsInAnswer(AgentResult result)
    {
        Assert.NotNull(result.StructuredData);
        Assert.Contains(JsonSerializer.Serialize(result.StructuredData), result.Answer, StringComparison.Ordinal);
    }

    private sealed class FakeFinanceMcpClient : IFinanceMcpClient
    {
        private static readonly SalesPeriod CurrentPeriod = new(new DateOnly(2026, 7, 13), DemoDate);
        private static readonly SalesPeriod PreviousPeriod = new(new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 12));

        public HistoricalYearlySalesResult Historical { get; init; } = new(
            [new(2021, 100m), new(2022, 200m), new(2023, 300m), new(2024, 400m), new(2025, 500m)],
            Array.Empty<string>());

        public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken) => Task.FromResult(Summary(CurrentPeriod, 1_200m));
        public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken) => Task.FromResult(new WeeklySalesComparison(Summary(CurrentPeriod, 1_200m), Summary(PreviousPeriod, 1_000m), 200m, 20m, SalesChangeDirection.Increased, Array.Empty<string>()));
        public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken) => Task.FromResult(new TopProductsResult(new SalesPeriod(new DateOnly(2026, 7, 1), DemoDate), Enumerable.Range(1, 5).Select(index => new TopProduct($"FIN-{index:000}", $"Product {index}", 1m, 100m - index, 50m)).ToArray(), Array.Empty<string>()));
        public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken) => Task.FromResult(Historical);
        public Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken) => Task.FromResult(new BudgetTargetResult(year, month, true, 3_000_000m, 1_000_000m, "budget", Array.Empty<string>()));

        private static SalesSummary Summary(SalesPeriod period, decimal revenue) => new(period, revenue, 400m, revenue - 400m, (revenue - 400m) / revenue * 100m, 4m, 2, revenue / 2m, new TopProduct("FIN-001", "Ledger Pro", 4m, revenue, revenue - 400m), Array.Empty<string>());
    }
}
