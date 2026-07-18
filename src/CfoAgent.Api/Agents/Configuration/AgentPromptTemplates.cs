using System.Text.Json;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;

namespace CfoAgent.Api.Agents.Configuration;

public static class AgentPromptTemplates
{
    private const string VerifiedDataInstructions = "Write a concise executive response using only VERIFIED_DATA. Do not calculate, change, or add financial values. Return prose only; do not return tool calls.";

    public static string ForClassification(string message) => $$"""
        Classify the final user request. Return exactly one intent name and no other text:
        SalesSummary, SalesComparison, TopProducts, Forecast, Knowledge, Mixed, or Unsupported.
        [MOCK:CLASSIFY]
        {{message}}
        """;

    public static string ForSalesSummary(SalesSummary summary) => Create("[MOCK:SALES_SUMMARY]", summary);

    public static string ForSalesComparison(WeeklySalesComparison comparison) => Create("[MOCK:SALES_COMPARISON]", comparison);

    public static string ForTopProducts(TopProductsResult topProducts) => Create("[MOCK:TOP_PRODUCTS]", topProducts);

    public static string ForForecast(SalesForecastResult forecast) => Create("[MOCK:FORECAST]", forecast);

    public static string ForKnowledge(string retrievedContext) => $$"""
        Answer concisely using only RETRIEVED_CONTEXT. Do not add facts, values, or sources. If the context is insufficient, say so. Return prose only; do not return tool calls.
        [MOCK:KNOWLEDGE]
        {{retrievedContext}}
        """;

    public static string ForOrchestration(IEnumerable<OrchestratedSpecialistResult> verifiedOutputs) => $$"""
        Combine the specialist results into one concise CFO response using only VERIFIED_DATA. Do not recalculate, change, or add financial values. Return prose only; do not return tool calls.
        [MOCK:ORCHESTRATE]
        {{JsonSerializer.Serialize(verifiedOutputs)}}
        """;

    private static string Create(string marker, object verifiedPayload) =>
        $"{VerifiedDataInstructions}\n{marker}\n{JsonSerializer.Serialize(verifiedPayload)}";
}
