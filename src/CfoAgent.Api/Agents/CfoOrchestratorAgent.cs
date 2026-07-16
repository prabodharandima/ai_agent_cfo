using System.Text.Json;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;

namespace CfoAgent.Api.Agents;

public sealed class CfoOrchestratorAgent(
    SalesAnalysisAgent salesAnalysisAgent,
    ForecastingAgent forecastingAgent,
    FinancialKnowledgeAgent financialKnowledgeAgent,
    CfoAgentFramework agentFramework)
{
    private const int MaximumSpecialistInvocations = 2;

    public async Task<CfoIntent> ClassifyAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var agent = agentFramework.CreateAgent(AgentDefinitions.CfoOrchestrator);
        var session = await agent.CreateSessionAsync(cancellationToken);
        var response = await agent.RunAsync($"[MOCK:CLASSIFY]\n{message}", session, options: null, cancellationToken);

        return Enum.TryParse<CfoIntent>(response.Text, ignoreCase: true, out var intent)
            ? intent
            : CfoIntent.Unsupported;
    }

    public async Task<AgentResult> HandleAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);

        try
        {
            var intent = await ClassifyAsync(request.Message, cancellationToken);
            var specialistResults = intent switch
            {
                CfoIntent.SalesSummary => [await salesAnalysisAgent.GetWeeklySummaryAsync(request, cancellationToken)],
                CfoIntent.SalesComparison => [await salesAnalysisAgent.GetWeekOverWeekComparisonAsync(request, cancellationToken)],
                CfoIntent.TopProducts => [await salesAnalysisAgent.GetCurrentMonthTopProductsAsync(request, cancellationToken)],
                CfoIntent.Forecast => [await forecastingAgent.GetForecastAsync(request, cancellationToken)],
                CfoIntent.Knowledge => [await financialKnowledgeAgent.AnswerAsync(request, cancellationToken: cancellationToken)],
                CfoIntent.Mixed => await GetMixedResultsAsync(request, cancellationToken),
                _ => Array.Empty<AgentResult>()
            };

            return specialistResults.Length == 0
                ? UnsupportedResult()
                : await ComposeAsync(specialistResults, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("The CFO orchestrator could not complete the request.", exception);
        }
    }

    private async Task<AgentResult[]> GetMixedResultsAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        var forecastTask = forecastingAgent.GetForecastAsync(request, cancellationToken);
        var knowledgeTask = financialKnowledgeAgent.AnswerAsync(request, cancellationToken: cancellationToken);
        var results = await Task.WhenAll(forecastTask, knowledgeTask);

        if (results.Length > MaximumSpecialistInvocations)
        {
            throw new InvalidOperationException("The configured specialist invocation limit was exceeded.");
        }

        return results;
    }

    private async Task<AgentResult> ComposeAsync(IReadOnlyList<AgentResult> specialistResults, CancellationToken cancellationToken)
    {
        var agent = agentFramework.CreateAgent(AgentDefinitions.CfoOrchestrator);
        var session = await agent.CreateSessionAsync(cancellationToken);
        var verifiedOutputs = specialistResults.Select(result => new OrchestratedSpecialistResult(
            result.AgentNames.Single(),
            result.ResponseType,
            result.StructuredData));
        var response = await agent.RunAsync(
            $"[MOCK:ORCHESTRATE]\n{JsonSerializer.Serialize(verifiedOutputs)}",
            session,
            options: null,
            cancellationToken);

        return new AgentResult(
            response.Text,
            specialistResults.Count == 1 ? specialistResults[0].ResponseType : AgentResponseType.Mixed,
            specialistResults.SelectMany(result => result.AgentNames).Distinct(StringComparer.Ordinal).ToArray(),
            specialistResults.Count == 1 ? specialistResults[0].StructuredData : verifiedOutputs.ToArray(),
            specialistResults.SelectMany(result => result.Sources).Distinct().ToArray(),
            specialistResults.SelectMany(result => result.Assumptions).Distinct(StringComparer.Ordinal).ToArray(),
            specialistResults.SelectMany(result => result.Warnings).Distinct(StringComparer.Ordinal).ToArray(),
            specialistResults.Select(result => result.DataPeriod).FirstOrDefault(period => period is not null));
    }

    private static AgentResult UnsupportedResult() => new(
        "This request is outside the supported CFO MVP scope. Ask about weekly sales, comparisons, top products, forecasts, or indexed financial knowledge.",
        AgentResponseType.Unsupported,
        [AgentDefinitions.CfoOrchestrator.Name],
        null,
        Array.Empty<AgentSource>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        null);
}
