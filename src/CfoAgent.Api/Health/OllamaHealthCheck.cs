using System.Diagnostics;
using System.Text.Json;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Health;

public sealed class OllamaHealthCheck : IHealthCheck
{
    private static readonly TimeSpan MaximumProbeTimeout = TimeSpan.FromSeconds(5);
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IOptions<AiOptions> aiOptions;
    private readonly ILogger<OllamaHealthCheck> logger;

    public OllamaHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<AiOptions> aiOptions,
        ILogger<OllamaHealthCheck>? logger = null)
    {
        this.httpClientFactory = httpClientFactory;
        this.aiOptions = aiOptions;
        this.logger = logger ?? NullLogger<OllamaHealthCheck>.Instance;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = aiOptions.Value;
        var stopwatch = Stopwatch.StartNew();

        if (!string.Equals(options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            LogOutcome(options, stopwatch, "Skipped", "NotSelected");
            return HealthCheckResult.Healthy("Ollama is not selected.");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(options.TimeoutSeconds > MaximumProbeTimeout.TotalSeconds
            ? MaximumProbeTimeout
            : TimeSpan.FromSeconds(options.TimeoutSeconds));

        try
        {
            var client = httpClientFactory.CreateClient(AiOptions.OllamaHttpClientName);
            using var response = await client.GetAsync("api/tags", timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                LogOutcome(options, stopwatch, "Failure", "Unavailable");
                return HealthCheckResult.Unhealthy("Ollama is unavailable.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: timeoutSource.Token);
            if (!ContainsConfiguredModel(document.RootElement, options.Model))
            {
                LogOutcome(options, stopwatch, "Failure", "ModelUnavailable");
                return HealthCheckResult.Unhealthy("Configured Ollama model is unavailable.");
            }

            LogOutcome(options, stopwatch, "Success", "None");
            return HealthCheckResult.Healthy("Ollama is ready.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogOutcome(options, stopwatch, "Cancelled", "CallerCancelled");
            throw;
        }
        catch (OperationCanceledException)
        {
            LogOutcome(options, stopwatch, "Failure", "Timeout");
            return HealthCheckResult.Unhealthy("Ollama health check timed out.");
        }
        catch (HttpRequestException)
        {
            LogOutcome(options, stopwatch, "Failure", "Unavailable");
            return HealthCheckResult.Unhealthy("Ollama is unavailable.");
        }
        catch (JsonException)
        {
            LogOutcome(options, stopwatch, "Failure", "InvalidResponse");
            return HealthCheckResult.Unhealthy("Ollama health check returned an invalid response.");
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

    private void LogOutcome(AiOptions options, Stopwatch stopwatch, string outcome, string failureCategory)
    {
        logger.LogInformation(
            "Ollama operation completed. Provider: {Provider}; Model: {Model}; Operation: {Operation}; DurationMilliseconds: {DurationMilliseconds}; Outcome: {Outcome}; FailureCategory: {FailureCategory}",
            options.Provider,
            options.Model,
            "readiness",
            stopwatch.ElapsedMilliseconds,
            outcome,
            failureCategory);
    }
}
