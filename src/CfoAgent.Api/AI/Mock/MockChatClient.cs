using System.Runtime.CompilerServices;
using System.Text.Json;
using CfoAgent.Api.Agents.Configuration;
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
    private const string OrchestrateMarker = "[MOCK:ORCHESTRATE]";
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
        if (options?.Tools is { Count: > 0 }
            && prompt.Contains(AgentPromptTemplates.McpToolSelectionMarker, StringComparison.Ordinal))
        {
            return CreateToolSelectionResponse(prompt, options.Tools, options.ModelId ?? Metadata.DefaultModelId);
        }

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

        if (TryGetPayload(prompt, OrchestrateMarker, out var orchestrationPayload))
        {
            return FormatVerifiedContext("Mock CFO orchestrated response", orchestrationPayload);
        }

        return "Mock response: This request is outside the CFO MVP scope.";
    }

    private static ChatResponse CreateToolSelectionResponse(string prompt, IList<AITool> tools, string? modelId)
    {
        var availableNames = tools.OfType<AIFunctionDeclaration>()
            .Select(tool => tool.Name)
            .ToHashSet(StringComparer.Ordinal);
        var selectedName = SelectToolName(prompt, availableNames);
        if (selectedName is null)
        {
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "No approved tool matches the request."))
            {
                ModelId = modelId
            };
        }

        var arguments = ParseCanonicalArguments(prompt);
        return new ChatResponse(new ChatMessage(
            ChatRole.Assistant,
            [new FunctionCallContent("mock-mcp-call", selectedName, arguments)]))
        {
            ModelId = modelId
        };
    }

    private static string? SelectToolName(string prompt, IReadOnlySet<string> availableNames)
    {
        var normalized = prompt.ToUpperInvariant();
        var preferred = normalized.Contains("COMPARE", StringComparison.Ordinal) || normalized.Contains("VERSUS", StringComparison.Ordinal)
            ? "compare_sales_periods"
            : normalized.Contains("TOP", StringComparison.Ordinal) && normalized.Contains("PRODUCT", StringComparison.Ordinal)
                ? "get_top_products"
                : normalized.Contains("FORECAST", StringComparison.Ordinal)
                    ? "get_historical_sales"
                    : normalized.Contains("READ", StringComparison.Ordinal)
                        ? "read_knowledge_file"
                        : normalized.Contains("TARGET", StringComparison.Ordinal) || normalized.Contains("BUDGET", StringComparison.Ordinal)
                            ? "get_budget_target"
                            : normalized.Contains("KNOWLEDGE", StringComparison.Ordinal) || normalized.Contains("FILE", StringComparison.Ordinal)
                                ? "list_knowledge_files"
                                : "get_sales_summary";

        return availableNames.Contains(preferred)
            ? preferred
            : availableNames.Count == 1 ? availableNames.Single() : null;
    }

    private static Dictionary<string, object?> ParseCanonicalArguments(string prompt)
    {
        var markerIndex = prompt.IndexOf(AgentPromptTemplates.McpCanonicalArgumentsMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return [];
        }

        var json = prompt[(markerIndex + AgentPromptTemplates.McpCanonicalArgumentsMarker.Length)..].Trim();
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
            ?? [];
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
