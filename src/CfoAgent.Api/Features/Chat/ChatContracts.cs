using System.Text.Json;
using CfoAgent.Api.Agents.Contracts;

namespace CfoAgent.Api.Features.Chat;

public sealed record ChatRequest(string? ConversationId, string? Message);

public sealed record ChatModel(string Provider, string Name);

public sealed record ChatResponse(
    string ConversationId,
    string Answer,
    IReadOnlyList<string> AgentNames,
    string ResponseType,
    JsonElement? StructuredData,
    IReadOnlyList<AgentSource> Sources,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Warnings,
    AgentDataPeriod? DataPeriod,
    ChatModel Model)
{
    public static ChatResponse FromAgentResult(AgentResult result, string conversationId, ChatModel model)
    {
        ArgumentNullException.ThrowIfNull(result);

        var agentNames = new[] { "CfoOrchestratorAgent" }
            .Concat(result.AgentNames)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new ChatResponse(
            conversationId,
            result.Answer,
            agentNames,
            ToResponseType(result.ResponseType),
            result.StructuredData is null ? null : JsonSerializer.SerializeToElement(result.StructuredData),
            result.Sources,
            result.Assumptions,
            result.Warnings,
            result.DataPeriod,
            model);
    }

    private static string ToResponseType(AgentResponseType responseType) => responseType switch
    {
        AgentResponseType.SalesSummary => "sales_summary",
        AgentResponseType.SalesComparison => "sales_comparison",
        AgentResponseType.TopProducts => "top_products",
        AgentResponseType.Forecast => "forecast",
        AgentResponseType.Knowledge => "knowledge",
        AgentResponseType.Mixed => "mixed",
        _ => "unsupported"
    };
}
