using System.Diagnostics;
using System.Text.Json;
using CfoAgent.Api.AI;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Rag.Retrieval;
using CfoAgent.Api.Observability;
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
    private const int MaximumClassificationResponseCharacters = 256;
    private static readonly JsonSerializerOptions StructuredOutputJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<CfoOrchestratorAgent> _logger = logger ?? NullLogger<CfoOrchestratorAgent>.Instance;

    public async Task<CfoIntent> ClassifyAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);

        var activity = AgentActivityTracing.Start("intent.classification", agent: AgentDefinitions.CfoOrchestrator.Name);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, AgentPromptTemplates.ForClassification(request.Message, request.SessionContext))],
                new ChatOptions
                {
                    Instructions = AgentDefinitions.CfoOrchestrator.SystemInstructions,
                    ResponseFormat = ChatResponseFormat.ForJsonSchema<IntentClassificationOutput>(
                        StructuredOutputJsonOptions,
                        "cfo_intent_classification",
                        "A validated CFO request intent.")
                },
                cancellationToken);

            var intent = TryParseStructuredIntent(response.Text, out var parsedIntent) && parsedIntent != CfoIntent.Unsupported
                ? parsedIntent
                : ClassifyDeterministically(request.Message);
            AgentActivityTracing.Complete(activity, "intent.classification", stopwatch, "Success", AgentDefinitions.CfoOrchestrator.Name);
            return intent;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AgentActivityTracing.Complete(activity, "intent.classification", stopwatch, "Cancelled", AgentDefinitions.CfoOrchestrator.Name);
            throw;
        }
        catch
        {
            AgentActivityTracing.Complete(activity, "intent.classification", stopwatch, "Failure", AgentDefinitions.CfoOrchestrator.Name);
            throw;
        }
    }

    public async Task<AgentResult> HandleAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);

        var intent = await ClassifyAsync(request, cancellationToken);
        return await HandleClassifiedAsync(request, intent, cancellationToken);
    }

    public async Task<AgentResult> HandleClassifiedAsync(
        AgentRequest request,
        CfoIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("CFO request routed. Intent: {Intent}", intent);
            var specialistResults = intent switch
            {
                CfoIntent.SalesSummary => [await ExecuteSpecialistAsync(AgentDefinitions.SalesAnalysis.Name, () => salesAnalysisAgent.GetWeeklySummaryAsync(request, cancellationToken), cancellationToken)],
                CfoIntent.SalesComparison => [await ExecuteSpecialistAsync(AgentDefinitions.SalesAnalysis.Name, () => salesAnalysisAgent.GetWeekOverWeekComparisonAsync(request, cancellationToken), cancellationToken)],
                CfoIntent.TopProducts => [await ExecuteSpecialistAsync(AgentDefinitions.SalesAnalysis.Name, () => salesAnalysisAgent.GetCurrentMonthTopProductsAsync(request, cancellationToken), cancellationToken)],
                CfoIntent.Forecast => [await ExecuteSpecialistAsync(AgentDefinitions.Forecasting.Name, () => forecastingAgent.GetForecastAsync(request, cancellationToken), cancellationToken)],
                CfoIntent.Knowledge => [await ExecuteSpecialistAsync(AgentDefinitions.FinancialKnowledge.Name, () => financialKnowledgeAgent.AnswerAsync(request, cancellationToken: cancellationToken), cancellationToken)],
                CfoIntent.Mixed => await GetMixedResultsAsync(request, cancellationToken),
                _ => Array.Empty<AgentResult>()
            };

            var compositionActivity = AgentActivityTracing.Start("result.composition", AgentDefinitions.CfoOrchestrator.Name);
            var compositionStopwatch = Stopwatch.StartNew();
            AgentResult result;
            try
            {
                result = specialistResults.Length == 0 ? UnsupportedResult() : resultComposer.Compose(specialistResults);
                AgentActivityTracing.Complete(compositionActivity, "result.composition", compositionStopwatch, "Success", AgentDefinitions.CfoOrchestrator.Name);
            }
            catch
            {
                AgentActivityTracing.Complete(compositionActivity, "result.composition", compositionStopwatch, "Failure", AgentDefinitions.CfoOrchestrator.Name);
                throw;
            }

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
        var forecastTask = ExecuteSpecialistAsync(AgentDefinitions.Forecasting.Name, () => forecastingAgent.GetForecastAsync(request, cancellationToken), cancellationToken);
        var knowledgeTask = ExecuteSpecialistAsync(AgentDefinitions.FinancialKnowledge.Name, () => financialKnowledgeAgent.AnswerAsync(request, cancellationToken: cancellationToken), cancellationToken);
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

    private static bool TryParseStructuredIntent(string? response, out CfoIntent intent)
    {
        intent = CfoIntent.Unsupported;
        if (string.IsNullOrWhiteSpace(response) || response.Length > MaximumClassificationResponseCharacters)
        {
            return false;
        }

        try
        {
            var output = JsonSerializer.Deserialize<IntentClassificationOutput>(response, StructuredOutputJsonOptions);
            return output is not null && TryParseIntent(output.Intent, out intent);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static async Task<AgentResult> ExecuteSpecialistAsync(string agentName, Func<Task<AgentResult>> operation, CancellationToken cancellationToken)
    {
        var activity = AgentActivityTracing.Start("specialist.execution", agentName);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await operation();
            AgentActivityTracing.Complete(activity, "specialist.execution", stopwatch, "Success", agentName);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AgentActivityTracing.Complete(activity, "specialist.execution", stopwatch, "Cancelled", agentName);
            throw;
        }
        catch
        {
            AgentActivityTracing.Complete(activity, "specialist.execution", stopwatch, "Failure", agentName);
            throw;
        }
    }

    private static bool TryParseIntent(string? candidate, out CfoIntent intent)
    {
        intent = CfoIntent.Unsupported;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        candidate = candidate.Trim();
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
