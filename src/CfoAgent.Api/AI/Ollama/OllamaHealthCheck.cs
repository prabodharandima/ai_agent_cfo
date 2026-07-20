using System.Diagnostics;
using System.Text.Json;
using CfoAgent.Api.AI;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace CfoAgent.Api.AI.Ollama;

public sealed class OllamaHealthCheck : IHealthCheck
{
    private static readonly TimeSpan MaximumProbeTimeout = TimeSpan.FromSeconds(5);
    private readonly IHttpClientFactory httpClientFactory;
    private readonly OllamaOptions options;
    private readonly AiProviderDescriptor provider;
    private readonly ILogger<OllamaHealthCheck> logger;

    public OllamaHealthCheck(
        IHttpClientFactory httpClientFactory,
        OllamaOptions options,
        AiProviderDescriptor provider,
        ILogger<OllamaHealthCheck>? logger = null)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options;
        this.provider = provider;
        this.logger = logger ?? NullLogger<OllamaHealthCheck>.Instance;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(options.TimeoutSeconds > MaximumProbeTimeout.TotalSeconds
            ? MaximumProbeTimeout
            : TimeSpan.FromSeconds(options.TimeoutSeconds));

        try
        {
            var client = httpClientFactory.CreateClient(OllamaOptions.HttpClientName);
            using var response = await client.GetAsync("api/tags", timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                LogOutcome(stopwatch, "Failure", "Unavailable");
                return HealthCheckResult.Unhealthy($"{provider.ProviderName} is unavailable.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: timeoutSource.Token);
            if (!ContainsConfiguredModel(document.RootElement, provider.ModelName))
            {
                LogOutcome(stopwatch, "Failure", "ModelUnavailable");
                return HealthCheckResult.Unhealthy($"Configured {provider.ProviderName} model is unavailable.");
            }

            LogOutcome(stopwatch, "Success", "None");
            return HealthCheckResult.Healthy($"{provider.ProviderName} is ready.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogOutcome(stopwatch, "Cancelled", "CallerCancelled");
            throw;
        }
        catch (OperationCanceledException)
        {
            LogOutcome(stopwatch, "Failure", "Timeout");
            return HealthCheckResult.Unhealthy($"{provider.ProviderName} health check timed out.");
        }
        catch (HttpRequestException)
        {
            LogOutcome(stopwatch, "Failure", "Unavailable");
            return HealthCheckResult.Unhealthy($"{provider.ProviderName} is unavailable.");
        }
        catch (JsonException)
        {
            LogOutcome(stopwatch, "Failure", "InvalidResponse");
            return HealthCheckResult.Unhealthy($"{provider.ProviderName} health check returned an invalid response.");
        }
    }

    private static bool ContainsConfiguredModel(JsonElement root, string model)
    {
        if (!root.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return models.EnumerateArray().Any(candidate =>
            candidate.ValueKind == JsonValueKind.Object
            && candidate.TryGetProperty("name", out var name)
            && string.Equals(name.GetString(), model, StringComparison.Ordinal));
    }

    private void LogOutcome(Stopwatch stopwatch, string outcome, string failureCategory)
    {
        logger.LogInformation(
            "AI provider operation completed. Provider: {Provider}; Model: {Model}; Operation: {Operation}; DurationMilliseconds: {DurationMilliseconds}; Outcome: {Outcome}; FailureCategory: {FailureCategory}",
            provider.ProviderName,
            provider.ModelName,
            "readiness",
            stopwatch.ElapsedMilliseconds,
            outcome,
            failureCategory);
    }
}
