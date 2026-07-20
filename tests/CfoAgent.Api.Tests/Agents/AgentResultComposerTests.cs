using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Contracts;

namespace CfoAgent.Api.Tests.Agents;

public sealed class AgentResultComposerTests
{
    private readonly AgentResultComposer _composer = new();

    [Fact]
    public void Compose_ReturnsSingleSpecialistResultWithoutRegeneratingIt()
    {
        var result = CreateResult("Verified sales answer.", AgentResponseType.SalesSummary, "Sales Analysis Agent", new { NetRevenue = 125m });

        var composed = _composer.Compose([result]);

        Assert.Same(result, composed);
        Assert.Equal("Verified sales answer.", composed.Answer);
    }

    [Fact]
    public void Compose_MergesMixedResultsOnceInStableWorkerOrder()
    {
        var forecastData = new { ExpectedRevenue = 500m };
        var knowledgeData = new { Assumption = "Stable unit economics" };
        var period = new AgentDataPeriod(new DateOnly(2021, 1, 1), new DateOnly(2025, 12, 31), "Historical sales");
        var source = new AgentSource("doc-1", "Forecast Assumptions", "Planning", "data/knowledge/forecast-assumptions.md", "2026-2030");
        var forecast = new AgentResult(
            "Verified forecast answer.",
            AgentResponseType.Forecast,
            ["Forecasting Agent"],
            forecastData,
            Array.Empty<AgentSource>(),
            ["Linear trend"],
            ["Historical data is limited"],
            period);
        var knowledge = new AgentResult(
            "Grounded knowledge answer.",
            AgentResponseType.Knowledge,
            ["Financial Knowledge Agent"],
            knowledgeData,
            [source, source],
            ["Linear trend", "Stable unit economics"],
            ["Historical data is limited", "Market risk remains"],
            null);

        var composed = _composer.Compose([forecast, knowledge]);

        Assert.Equal("Verified forecast answer.\n\nGrounded knowledge answer.", composed.Answer);
        Assert.Equal(AgentResponseType.Mixed, composed.ResponseType);
        Assert.Equal(["Forecasting Agent", "Financial Knowledge Agent"], composed.AgentNames);
        Assert.Equal(["Linear trend", "Stable unit economics"], composed.Assumptions);
        Assert.Equal(["Historical data is limited", "Market risk remains"], composed.Warnings);
        Assert.Equal(period, composed.DataPeriod);
        Assert.Equal(source, Assert.Single(composed.Sources));

        var structured = Assert.IsType<OrchestratedSpecialistResult[]>(composed.StructuredData);
        Assert.Equal(2, structured.Length);
        Assert.Same(forecastData, structured[0].StructuredData);
        Assert.Same(knowledgeData, structured[1].StructuredData);
        Assert.Equal(AgentResponseType.Forecast, structured[0].ResponseType);
        Assert.Equal(AgentResponseType.Knowledge, structured[1].ResponseType);
    }

    [Fact]
    public void Compose_RejectsAnEmptyResultSet()
    {
        var exception = Assert.Throws<ArgumentException>(() => _composer.Compose([]));

        Assert.Equal("specialistResults", exception.ParamName);
    }

    private static AgentResult CreateResult(string answer, AgentResponseType responseType, string agentName, object data) => new(
        answer,
        responseType,
        [agentName],
        data,
        Array.Empty<AgentSource>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        null);
}
