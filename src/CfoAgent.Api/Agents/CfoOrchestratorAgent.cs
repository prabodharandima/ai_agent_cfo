using System.Diagnostics;
using CfoAgent.Api.AI;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CfoAgent.Api.Agents;

public sealed class CfoOrchestratorAgent(
    SalesAnalysisAgent salesAnalysisAgent,
    ForecastingAgent forecastingAgent,
    FinancialKnowledgeAgent financialKnowledgeAgent,
    AgentResultComposer resultComposer,
    IChatClient chatClient,
    ILogger<CfoOrchestratorAgent>? logger = null)
{
    private const int MaximumClassificationResponseCharacters = 64;
    private readonly ILogger<CfoOrchestratorAgent> _logger = logger ?? NullLogger<CfoOrchestratorAgent>.Instance;

    public async Task<CfoIntent> ClassifyAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, AgentPromptTemplates.ForClassification(message))],
            new ChatOptions { Instructions = AgentDefinitions.CfoOrchestrator.SystemInstructions },
            cancellationToken);

        return TryParseIntent(response.Text, out var intent) && intent != CfoIntent.Unsupported
            ? intent
            : ClassifyDeterministically(message);
    }

    public async Task<AgentResult> HandleAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var intent = await ClassifyAsync(request.Message, cancellationToken);
            _logger.LogInformation("CFO request routed. Intent: {Intent}", intent);
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

            var result = specialistResults.Length == 0
                ? UnsupportedResult()
                : resultComposer.Compose(specialistResults);

            _logger.LogInformation(
                "CFO request completed. ResponseType: {ResponseType}; AgentCount: {AgentCount}; DurationMilliseconds: {DurationMilliseconds}",
                result.ResponseType,
                result.AgentNames.Count,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("CFO request cancelled. DurationMilliseconds: {DurationMilliseconds}", stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (LlmDependencyException)
        {
            throw;
        }
        catch (McpDependencyException)
        {
            throw;
        }
        catch (VectorSearchDependencyException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("CFO request failed. FailureType: {FailureType}; DurationMilliseconds: {DurationMilliseconds}", exception.GetType().Name, stopwatch.ElapsedMilliseconds);
            throw new InvalidOperationException("The CFO orchestrator could not complete the request.", exception);
        }
    }

    private async Task<AgentResult[]> GetMixedResultsAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        var forecastTask = forecastingAgent.GetForecastAsync(request, cancellationToken);
        var knowledgeTask = financialKnowledgeAgent.AnswerAsync(request, cancellationToken: cancellationToken);
        var results = await Task.WhenAll(forecastTask, knowledgeTask);

        return results;
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

    private static bool TryParseIntent(string? response, out CfoIntent intent)
    {
        intent = CfoIntent.Unsupported;
        if (string.IsNullOrWhiteSpace(response) || response.Length > MaximumClassificationResponseCharacters)
        {
            return false;
        }

        var candidate = response.Trim();
        foreach (var allowedIntent in Enum.GetValues<CfoIntent>())
        {
            if (string.Equals(candidate, allowedIntent.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                intent = allowedIntent;
                return true;
            }
        }

        return false;
    }

    private static CfoIntent ClassifyDeterministically(string message)
    {
        var normalized = message.ToUpperInvariant();
        var hasForecast = normalized.Contains("FORECAST", StringComparison.Ordinal);
        var hasKnowledge = normalized.Contains("TARGET", StringComparison.Ordinal)
            || normalized.Contains("ASSUMPTION", StringComparison.Ordinal)
            || normalized.Contains("RISK", StringComparison.Ordinal);

        if (hasForecast && hasKnowledge)
        {
            return CfoIntent.Mixed;
        }

        if (hasForecast)
        {
            return CfoIntent.Forecast;
        }

        if (normalized.Contains("COMPARE", StringComparison.Ordinal) || normalized.Contains("VERSUS", StringComparison.Ordinal))
        {
            return CfoIntent.SalesComparison;
        }

        if (normalized.Contains("TOP", StringComparison.Ordinal) && normalized.Contains("PRODUCT", StringComparison.Ordinal))
        {
            return CfoIntent.TopProducts;
        }

        if (hasKnowledge)
        {
            return CfoIntent.Knowledge;
        }

        if (normalized.Contains("SALES", StringComparison.Ordinal) || normalized.Contains("WEEK", StringComparison.Ordinal))
        {
            return CfoIntent.SalesSummary;
        }

        return CfoIntent.Unsupported;
    }
}
