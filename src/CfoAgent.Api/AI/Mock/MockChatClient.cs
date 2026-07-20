using System.Runtime.CompilerServices;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.AI.Mock;

public sealed class MockChatClient : IChatClient
{
    private const string ClassificationMarker = "[MOCK:CLASSIFY]";
    private const string SalesSummaryMarker = "[MOCK:SALES_SUMMARY]";
    private const string SalesComparisonMarker = "[MOCK:SALES_COMPARISON]";
    private const string TopProductsMarker = "[MOCK:TOP_PRODUCTS]";
    private const string ForecastMarker = "[MOCK:FORECAST]";
    private const string KnowledgeMarker = "[MOCK:KNOWLEDGE]";
    private readonly AiOptions options;

    public MockChatClient(IOptions<AiOptions> options)
    {
        this.options = options.Value;
        Metadata = new ChatClientMetadata("Mock", new Uri("https://mock.local"), this.options.Model);
    }

    public ChatClientMetadata Metadata { get; }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        await ApplySimulationAsync(cancellationToken);

        var prompt = string.Join("\n", messages.Select(message => message.Text ?? string.Empty)).Trim();
        var responseText = CreateResponse(prompt);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))
        {
            ModelId = options?.ModelId ?? Metadata.DefaultModelId
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text)
        {
            ModelId = response.ModelId
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        return serviceType == typeof(ChatClientMetadata) ? Metadata
            : serviceType.IsInstanceOfType(this) ? this
            : null;
    }

    public void Dispose()
    {
    }

    private async Task ApplySimulationAsync(CancellationToken cancellationToken)
    {
        if (options.SimulateFailure)
        {
            throw new InvalidOperationException("Mock chat failure was requested by configuration.");
        }

        if (options.SimulatedDelayMilliseconds > 0)
        {
            await Task.Delay(options.SimulatedDelayMilliseconds, cancellationToken);
        }
    }

    private static string CreateResponse(string prompt)
    {
        if (TryGetPayload(prompt, ClassificationMarker, out var classificationRequest))
        {
            return ClassifyIntent(classificationRequest);
        }

        if (TryGetPayload(prompt, SalesSummaryMarker, out var salesPayload))
        {
            return FormatVerifiedContext("Mock sales executive summary", salesPayload);
        }

        if (TryGetPayload(prompt, SalesComparisonMarker, out var comparisonPayload))
        {
            return FormatVerifiedContext("Mock sales comparison", comparisonPayload);
        }

        if (TryGetPayload(prompt, TopProductsMarker, out var topProductsPayload))
        {
            return FormatVerifiedContext("Mock top-products explanation", topProductsPayload);
        }

        if (TryGetPayload(prompt, ForecastMarker, out var forecastPayload))
        {
            return FormatVerifiedContext("Mock forecast explanation", forecastPayload);
        }

        if (TryGetPayload(prompt, KnowledgeMarker, out var knowledgePayload))
        {
            return FormatVerifiedContext("Mock knowledge answer", knowledgePayload);
        }

        return "Mock response: This request is outside the CFO MVP scope.";
    }

    private static bool TryGetPayload(string prompt, string marker, out string payload)
    {
        var markerIndex = prompt.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            payload = string.Empty;
            return false;
        }

        payload = prompt[(markerIndex + marker.Length)..].Trim();
        return true;
    }

    private static string ClassifyIntent(string request)
    {
        var normalized = request.ToUpperInvariant();
        var hasForecast = normalized.Contains("FORECAST", StringComparison.Ordinal);
        var hasKnowledge = normalized.Contains("TARGET", StringComparison.Ordinal)
            || normalized.Contains("ASSUMPTION", StringComparison.Ordinal)
            || normalized.Contains("RISK", StringComparison.Ordinal);
        var hasSales = normalized.Contains("SALES", StringComparison.Ordinal) || normalized.Contains("WEEK", StringComparison.Ordinal);

        if (hasForecast && hasKnowledge)
        {
            return "Mixed";
        }

        if (hasForecast)
        {
            return "Forecast";
        }

        if (normalized.Contains("COMPARE", StringComparison.Ordinal) || normalized.Contains("VERSUS", StringComparison.Ordinal))
        {
            return "SalesComparison";
        }

        if (normalized.Contains("TOP", StringComparison.Ordinal) && normalized.Contains("PRODUCT", StringComparison.Ordinal))
        {
            return "TopProducts";
        }

        if (hasKnowledge)
        {
            return "Knowledge";
        }

        if (hasSales)
        {
            return "SalesSummary";
        }

        return "Unsupported";
    }

    private static string FormatVerifiedContext(string heading, string context) => $"{heading} based only on verified context:\n{context}";
}
