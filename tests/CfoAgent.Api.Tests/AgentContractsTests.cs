using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.AI;

namespace CfoAgent.Api.Tests;

public sealed class AgentContractsTests
{
    [Fact]
    public void DefinitionsHaveUniqueNamesAndShareTheFinanceGuardrail()
    {
        var definitions = new[]
        {
            AgentDefinitions.CfoOrchestrator,
            AgentDefinitions.SalesAnalysis,
            AgentDefinitions.Forecasting,
            AgentDefinitions.FinancialKnowledge
        };

        Assert.Equal(definitions.Length, definitions.Select(definition => definition.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(definitions, definition => Assert.Contains(AgentDefinitions.SharedGuardrail, definition.SystemInstructions, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SpecialistUsesStandardChatClientWithItsBoundedInstructions()
    {
        using var client = new RecordingChatClient();
        var agent = new SalesAnalysisAgent(client, new FinanceFake());

        var result = await agent.GetWeeklySummaryAsync(
            new AgentRequest("Give me this week's sales summary."),
            CancellationToken.None);

        Assert.Equal("Verified presentation.", result.Answer);
        Assert.Equal(AgentDefinitions.SalesAnalysis.SystemInstructions, Assert.Single(client.Options).Instructions);
        Assert.True(Assert.Single(client.Options).Tools is null or { Count: 0 });
        Assert.Contains("[MOCK:SALES_SUMMARY]", Assert.Single(client.Prompts), StringComparison.Ordinal);
    }

    [Fact]
    public void AgentConstructorsDependOnPortsRatherThanTransportImplementations()
    {
        var agentTypes = new[]
        {
            typeof(CfoOrchestratorAgent),
            typeof(SalesAnalysisAgent),
            typeof(ForecastingAgent),
            typeof(FinancialKnowledgeAgent)
        };
        var parameterTypeNames = agentTypes
            .SelectMany(type => type.GetConstructors().Single().GetParameters())
            .Select(parameter => parameter.ParameterType.FullName ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(parameterTypeNames, name => name.StartsWith("ModelContextProtocol.", StringComparison.Ordinal));
        Assert.DoesNotContain(parameterTypeNames, name => name.StartsWith("CfoAgent.Api.Rag.Chroma.", StringComparison.Ordinal));
        Assert.DoesNotContain(parameterTypeNames, name => name.StartsWith("CfoAgent.Api.AI.Ollama.", StringComparison.Ordinal));
    }

    [Fact]
    public void AgentResultCarriesOnlyProviderIndependentResponseData()
    {
        var result = new AgentResult(
            "Verified answer",
            AgentResponseType.Forecast,
            [AgentDefinitions.Forecasting.Name],
            new { forecastYear = 2027, expectedRevenue = 400m },
            [new AgentSource("forecast-assumptions", "Forecast Assumptions", "Method", "data/knowledge/forecast-assumptions.md")],
            ["Deterministic forecast"],
            Array.Empty<string>(),
            new AgentDataPeriod(new DateOnly(2021, 1, 1), new DateOnly(2025, 12, 31), "Historical period"));

        Assert.Equal(AgentResponseType.Forecast, result.ResponseType);
        Assert.Equal(AgentDefinitions.Forecasting.Name, Assert.Single(result.AgentNames));
        Assert.Equal("forecast-assumptions", Assert.Single(result.Sources).DocumentId);
        Assert.Equal("Historical period", result.DataPeriod?.Label);
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public List<string> Prompts { get; } = [];

        public List<ChatOptions> Options { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Prompts.Add(string.Join('\n', messages.Select(message => message.Text ?? string.Empty)));
            Options.Add(Assert.IsType<ChatOptions>(options));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Verified presentation.")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class FinanceFake : IFinanceMcpClient
    {
        public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken) => Task.FromResult(new SalesSummary(
            new SalesPeriod(new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 15)),
            200m,
            80m,
            120m,
            60m,
            2m,
            1,
            200m,
            null,
            Array.Empty<string>()));

        public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
