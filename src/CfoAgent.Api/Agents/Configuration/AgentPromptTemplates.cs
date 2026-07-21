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
        - SalesSummary: sales summaries for today, yesterday, a specified date, or the current week.
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

    public static string ForSalesSummaryDateRange(string message, DateOnly currentDate) => $$"""
        Interpret the user's requested inclusive sales-summary date range.
        The current date is {{currentDate:yyyy-MM-dd}}.

        Return exactly one JSON object and no Markdown, prose, calculation, or explanation:
        {"startDate":"YYYY-MM-DD","endDate":"YYYY-MM-DD"}

        Use these rules:
        - "today" means the current date only.
        - "yesterday" means the day before the current date only.
        - "since yesterday" means yesterday through the current date.
        - "this week" means Monday through the current date.
        - "last week" means the previous Monday through Sunday.
        - An explicit date means that date only unless the user asks for a range.
        - Do not return dates after the current date.

        SALES_SUMMARY_PERIOD_REQUEST:
        {{message}}
        """;

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
