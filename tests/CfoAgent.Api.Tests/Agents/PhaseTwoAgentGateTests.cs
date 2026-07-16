using System.Text.Json;
using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Data.Seed;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Tests.Finance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Agents;

public class PhaseTwoAgentGateTests
{
    private static readonly DateOnly DemoDate = new(2026, 7, 15);
    private static readonly TimeProvider Clock = new FixedTimeProvider(DemoDate);

    [Fact]
    public async Task SalesAgentUsesSeededDataAndIncludesTheVerifiedPayloadInItsAnswer()
    {
        await using var database = await CreateSeededDatabaseAsync();
        using var client = CreateClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateSalesAgent(database, client, services);
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
    public async Task ForecastAgentReturnsSeededForecastAssumptionsAndVerifiedPayload()
    {
        await using var database = await CreateSeededDatabaseAsync();
        using var client = CreateClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateForecastingAgent(database, client, services);

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
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        using var client = CreateClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateForecastingAgent(database, client, services);

        var result = await agent.GetForecastAsync(new AgentRequest("Give me the sales forecast."), CancellationToken.None);

        var forecast = Assert.IsType<SalesForecastResult>(result.StructuredData);
        Assert.Empty(forecast.Forecasts);
        Assert.NotEmpty(result.Warnings);
        AssertPayloadIsInAnswer(result);
    }

    [Fact]
    public async Task SalesAgentPropagatesCancellationFromTheMockClient()
    {
        await using var database = await CreateSeededDatabaseAsync();
        using var client = CreateClient(simulatedDelayMilliseconds: 5_000);
        using var services = new ServiceCollection().BuildServiceProvider();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(TimeSpan.FromMilliseconds(25));
        var agent = CreateSalesAgent(database, client, services);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            agent.GetWeeklySummaryAsync(new AgentRequest("Give me this week's sales summary."), cancellationSource.Token));
    }

    [Fact]
    public async Task ForecastAgentWrapsSimulatedMockFailureAsAControlledAgentError()
    {
        await using var database = await CreateSeededDatabaseAsync();
        using var client = CreateClient(simulateFailure: true);
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateForecastingAgent(database, client, services);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            agent.GetForecastAsync(new AgentRequest("Give me the sales forecast."), CancellationToken.None));

        Assert.Equal("The forecasting agent could not produce a forecast.", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private static async Task<TemporaryFinanceDatabase> CreateSeededDatabaseAsync()
    {
        var database = await TemporaryFinanceDatabase.CreateAsync();
        var seeder = new DevelopmentFinanceSeeder(database.Context, Options.Create(new FinanceOptions { DemoDate = DemoDate }));
        await seeder.SeedAsync(CancellationToken.None);
        return database;
    }

    private static SalesAnalysisAgent CreateSalesAgent(TemporaryFinanceDatabase database, MockChatClient client, IServiceProvider services)
    {
        var analysisService = new SalesAnalysisService(database.Context, Clock);
        return new SalesAnalysisAgent(analysisService, new CfoAgentFramework(client, NullLoggerFactory.Instance, services));
    }

    private static ForecastingAgent CreateForecastingAgent(TemporaryFinanceDatabase database, MockChatClient client, IServiceProvider services)
    {
        var analysisService = new SalesAnalysisService(database.Context, Clock);
        var forecastingService = new SalesForecastingService(analysisService, Clock);
        return new ForecastingAgent(forecastingService, new CfoAgentFramework(client, NullLoggerFactory.Instance, services));
    }

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
}
