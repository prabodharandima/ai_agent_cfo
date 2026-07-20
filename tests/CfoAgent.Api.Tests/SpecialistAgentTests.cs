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

namespace CfoAgent.Api.Tests;

public sealed class SpecialistAgentTests
{
    [Fact]
    public async Task SalesAgentReturnsVerifiedMcpSummaryComparisonAndTopProducts()
    {
        using var client = CreateClient();
        var mcp = new FinanceFake();
        var agent = new SalesAnalysisAgent(client, mcp);
        var request = new AgentRequest("Show finance data.");

        var summary = await agent.GetWeeklySummaryAsync(request, CancellationToken.None);
        var comparison = await agent.GetWeekOverWeekComparisonAsync(request, CancellationToken.None);
        var topProducts = await agent.GetCurrentMonthTopProductsAsync(request, CancellationToken.None);

        AssertAgentResult(summary, AgentResponseType.SalesSummary, AgentDefinitions.SalesAnalysis.Name);
        AssertAgentResult(comparison, AgentResponseType.SalesComparison, AgentDefinitions.SalesAnalysis.Name);
        AssertAgentResult(topProducts, AgentResponseType.TopProducts, AgentDefinitions.SalesAnalysis.Name);
        Assert.Equal(200m, Assert.IsType<SalesSummary>(summary.StructuredData).NetRevenue);
        Assert.Equal(100m, Assert.IsType<WeeklySalesComparison>(comparison.StructuredData).NetRevenueChange);
        Assert.Single(Assert.IsType<TopProductsResult>(topProducts.StructuredData).Products);
    }

    [Fact]
    public async Task ForecastingAgentReturnsFiveDeterministicForecastYearsFromMcpHistory()
    {
        using var client = CreateClient();
        var mcp = new FinanceFake();
        var agent = new ForecastingAgent(new SalesForecastingService(), client, mcp);

        var result = await agent.GetForecastAsync(new AgentRequest("Give me a forecast."), CancellationToken.None);

        AssertAgentResult(result, AgentResponseType.Forecast, AgentDefinitions.Forecasting.Name);
        var forecast = Assert.IsType<SalesForecastResult>(result.StructuredData);
        Assert.Equal([2026, 2027, 2028, 2029, 2030], forecast.Forecasts.Select(row => row.Year));
        Assert.Equal(5, forecast.Forecasts.Count);
        Assert.Equal(JsonSerializer.Serialize(forecast), result.Answer.Split('\n', 2)[1]);
        Assert.NotEmpty(result.Assumptions);
    }

    private static void AssertAgentResult(AgentResult result, AgentResponseType responseType, string agentName)
    {
        Assert.Equal(responseType, result.ResponseType);
        Assert.Equal(agentName, Assert.Single(result.AgentNames));
        Assert.NotNull(result.StructuredData);
        Assert.Contains("based only on verified context", result.Answer, StringComparison.Ordinal);
    }

    private static MockChatClient CreateClient() => new(Options.Create(new AiOptions { Provider = "Mock", Model = "DeterministicMock" }));

    private sealed class FinanceFake : IFinanceMcpClient
    {
        private static readonly SalesPeriod Current = new(new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 15));
        public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken) => Task.FromResult(Summary(Current, 200m));
        public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken) => Task.FromResult(new WeeklySalesComparison(Summary(Current, 200m), Summary(new SalesPeriod(new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 12)), 100m), 100m, 100m, SalesChangeDirection.Increased, Array.Empty<string>()));
        public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken) => Task.FromResult(new TopProductsResult(new SalesPeriod(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15)), [new TopProduct("FIN-001", "Ledger Pro", 2m, 200m, 120m)], Array.Empty<string>()));
        public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken) => Task.FromResult(new HistoricalYearlySalesResult([new(2021, 100m), new(2022, 200m), new(2023, 300m), new(2024, 400m), new(2025, 500m)], Array.Empty<string>()));
        public Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken) => throw new NotSupportedException();
        private static SalesSummary Summary(SalesPeriod period, decimal revenue) => new(period, revenue, 80m, revenue - 80m, (revenue - 80m) / revenue * 100m, 2m, 1, revenue, new TopProduct("FIN-001", "Ledger Pro", 2m, revenue, revenue - 80m), Array.Empty<string>());
    }
}
