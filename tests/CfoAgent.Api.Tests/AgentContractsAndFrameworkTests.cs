using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests;

public class AgentContractsAndFrameworkTests
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
    public async Task FrameworkCreatesAnInMemorySessionForTheMockChatClient()
    {
        using var client = new MockChatClient(Options.Create(new AiOptions
        {
            Provider = "Mock",
            Model = "DeterministicMock"
        }));
        using var services = new ServiceCollection().BuildServiceProvider();
        var framework = new CfoAgentFramework(client, NullLoggerFactory.Instance, services);

        var agent = framework.CreateAgent(AgentDefinitions.SalesAnalysis);
        var session = await agent.CreateSessionAsync(CancellationToken.None);

        Assert.Equal(AgentDefinitions.SalesAnalysis.Name, agent.Name);
        Assert.Contains(AgentDefinitions.SharedGuardrail, agent.Instructions, StringComparison.Ordinal);
        Assert.NotNull(session);
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
}
