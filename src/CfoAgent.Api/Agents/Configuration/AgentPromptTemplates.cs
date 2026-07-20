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

        Route requests using these rules:
        - SalesSummary: current or weekly sales summaries.
        - SalesComparison: comparisons between sales periods.
        - TopProducts: product rankings or top products.
        - Forecast: sales forecasts without a request for document-based assumptions or risks.
        - Knowledge: questions about documented financial targets, assumptions, risks, market risks, or product strategy.
        - Mixed: a forecast request that also asks about assumptions, targets, or risks.
        - Unsupported: only when none of the above applies.

        Examples:
        - "What is the annual sales target and what assumptions were used?" => Knowledge
        - "What financial risks are documented for the business?" => Knowledge
        - "Give me the forecast with assumptions and risks." => Mixed

        USER_REQUEST:
        {{message}}
        """;

    public static string ForSalesSummary(SalesSummary summary) => Create(summary);

    public static string ForSalesComparison(WeeklySalesComparison comparison) => Create(comparison);

    public static string ForTopProducts(TopProductsResult topProducts) => Create(topProducts);

    public static string ForForecast(SalesForecastResult forecast) => Create(forecast);

    public static string ForKnowledge(string retrievedContext) => $$"""
        Answer concisely using only RETRIEVED_CONTEXT. Do not add facts, values, or sources. If the context is insufficient, say so. Return prose only; do not return tool calls.
        RETRIEVED_CONTEXT:
        {{retrievedContext}}
        """;

    private static string Create(object verifiedPayload) =>
        $"{VerifiedDataInstructions}\nVERIFIED_DATA:\n{JsonSerializer.Serialize(verifiedPayload)}";
}
