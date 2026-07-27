using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CfoAgent.Api.AI;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Caching;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Rag.Retrieval;
using CfoAgent.Api.Observability;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Agents;

public sealed class CfoOrchestratorAgent(
    SalesAnalysisAgent salesAnalysisAgent,
    ForecastingAgent forecastingAgent,
    FinancialKnowledgeAgent financialKnowledgeAgent,
    AgentResultComposer resultComposer,
    IChatClient chatClient,
    ILogger<CfoOrchestratorAgent>? logger = null,
    IApplicationCache? applicationCache = null,
    IOptions<CacheOptions>? cacheOptions = null,
    AiProviderDescriptor? aiProvider = null,
    IOptions<AgentMiddlewareOptions>? agentMiddlewareOptions = null)
{
    private const int MaximumClassificationResponseCharacters = 256;
    private static readonly JsonSerializerOptions StructuredOutputJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<CfoOrchestratorAgent> _logger = logger ?? NullLogger<CfoOrchestratorAgent>.Instance;
    private readonly LlmClassificationCacheOptions classificationCacheOptions = cacheOptions?.Value.Classification ?? new();
    private readonly AgentMiddlewareOptions promptRiskOptions = agentMiddlewareOptions?.Value ?? new();

    public async Task<CfoIntent> ClassifyAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);

        var activity = AgentActivityTracing.Start("intent.classification", agent: AgentDefinitions.CfoOrchestrator.Name);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var modelIntent = await TryGetValidatedModelIntentAsync(request, cancellationToken);
            var intent = modelIntent ?? ClassifyDeterministically(request.Message);
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

    private async Task<CfoIntent?> TryGetValidatedModelIntentAsync(
        AgentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (applicationCache is null)
            {
                return await GetValidatedModelIntentAsync(request, cancellationToken);
            }

            var (providerName, modelName) = GetProviderIdentity();
            var cacheKey = CreateClassificationCacheKey(
                request.Message,
                request.SessionContext,
                providerName,
                modelName,
                classificationCacheOptions,
                promptRiskOptions);
            return await applicationCache.GetOrCreateAsync(
                cacheKey,
                TimeSpan.FromSeconds(classificationCacheOptions.TtlSeconds),
                token => GetValidatedModelIntentAsync(request, token),
                cancellationToken);
        }
        catch (ClassificationOutputNotCacheableException)
        {
            return null;
        }
    }

    private async Task<CfoIntent> GetValidatedModelIntentAsync(
        AgentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, AgentPromptTemplates.ForClassification(request.Message, request.SessionContext))],
            new ChatOptions
            {
                Instructions = AgentDefinitions.CfoOrchestrator.SystemInstructions,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<IntentClassificationOutput>(
                    new JsonSerializerOptions(JsonSerializerDefaults.Web),
                    "cfo_intent_classification",
                    "A validated CFO request intent.")
            },
            cancellationToken);

        if (!TryParseStructuredIntent(response.Text, out var intent) || intent == CfoIntent.Unsupported)
        {
            throw new ClassificationOutputNotCacheableException();
        }

        return intent;
    }

    private (string ProviderName, string ModelName) GetProviderIdentity()
    {
        if (aiProvider is not null)
        {
            return (aiProvider.ProviderName, aiProvider.ModelName);
        }

        var metadata = chatClient.GetService(typeof(ChatClientMetadata)) as ChatClientMetadata;
        return (
            string.IsNullOrWhiteSpace(metadata?.ProviderName) ? "unknown" : metadata.ProviderName,
            string.IsNullOrWhiteSpace(metadata?.DefaultModelId) ? "unknown" : metadata.DefaultModelId);
    }

    internal static string CreateClassificationCacheKey(
        string message,
        AgentSessionContext? sessionContext,
        string providerName,
        string modelName,
        LlmClassificationCacheOptions cacheOptions,
        AgentMiddlewareOptions promptRiskOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(cacheOptions);
        ArgumentNullException.ThrowIfNull(promptRiskOptions);

        var allowedIntents = string.Join(',', Enum.GetNames<CfoIntent>().OrderBy(name => name, StringComparer.Ordinal));
        return string.Join(
            ':',
            "llm-classification",
            Sha256(providerName),
            Sha256(modelName),
            Sha256(cacheOptions.PromptVersion),
            Sha256($"{cacheOptions.AllowedIntentSetVersion}\n{allowedIntents}"),
            CreateSessionContextFingerprint(sessionContext),
            Sha256(NormalizeMessage(message)),
            CreatePromptRiskPolicyFingerprint(promptRiskOptions));
    }

    private static string CreateSessionContextFingerprint(AgentSessionContext? sessionContext)
    {
        if (sessionContext is not { Turns.Count: > 0 })
        {
            return "stateless";
        }

        var serializedTurns = string.Join(
            '\n',
            sessionContext.Turns.Select(turn =>
            {
                var period = turn.DataPeriod;
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{turn.ResponseType}|{period?.From:yyyyMMdd}|{period?.To:yyyyMMdd}|{(period is null ? "none" : Sha256(period.Label ?? string.Empty))}");
            }));
        return Sha256(serializedTurns);
    }

    private static string CreatePromptRiskPolicyFingerprint(AgentMiddlewareOptions options)
    {
        var normalizedPhrases = string.Join(
            '\n',
            options.SuspiciousPromptPhrases
                .Where(phrase => !string.IsNullOrWhiteSpace(phrase))
                .Select(phrase => phrase.Trim())
                .OrderBy(phrase => phrase, StringComparer.OrdinalIgnoreCase));
        return Sha256($"{options.PromptInjectionCheckEnabled}\n{normalizedPhrases}");
    }

    private static string NormalizeMessage(string message) => string.Join(
        ' ',
        message.Normalize(NormalizationForm.FormKC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class ClassificationOutputNotCacheableException : Exception;

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
