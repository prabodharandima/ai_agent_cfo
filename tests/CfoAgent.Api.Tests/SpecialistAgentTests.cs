using System.Text.Json;
using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Tests.Finance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests;

public class SpecialistAgentTests
{
    private static readonly DateOnly DemoDate = new(2026, 7, 15);
    private static readonly TimeProvider Clock = new FixedTimeProvider(DemoDate);

    [Fact]
    public async Task SalesAgentReturnsVerifiedSummaryComparisonAndTopProducts()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var product = await database.AddProductAsync("FIN-001", "Ledger Pro");
        database.AddSale(product, "PREVIOUS", new DateOnly(2026, 7, 8), 1, 100m, 0m, 40m);
        database.AddSale(product, "CURRENT", new DateOnly(2026, 7, 14), 2, 100m, 0m, 40m);
        await database.SaveChangesAsync();
        using var client = CreateClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var analysisService = new SalesAnalysisService(database.Context, Clock);
        var agent = new SalesAnalysisAgent(analysisService, new CfoAgentFramework(client, NullLoggerFactory.Instance, services));
        var request = new AgentRequest("Show finance data.", "sales-session");

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
    public async Task ForecastingAgentReturnsFiveVerifiedForecastYears()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var product = await database.AddProductAsync("FIN-001", "Ledger Pro");
        database.AddSale(product, "2023", new DateOnly(2023, 6, 1), 1, 100m, 0m, 40m);
        database.AddSale(product, "2024", new DateOnly(2024, 6, 1), 1, 200m, 0m, 40m);
        database.AddSale(product, "2025", new DateOnly(2025, 6, 1), 1, 300m, 0m, 40m);
        await database.SaveChangesAsync();
        using var client = CreateClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var analysisService = new SalesAnalysisService(database.Context, Clock);
        var forecastingService = new SalesForecastingService(analysisService, Clock);
        var agent = new ForecastingAgent(forecastingService, new CfoAgentFramework(client, NullLoggerFactory.Instance, services));

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

    private static MockChatClient CreateClient() => new(Options.Create(new AiOptions
    {
        Provider = "Mock",
        Model = "DeterministicMock"
    }));
}
