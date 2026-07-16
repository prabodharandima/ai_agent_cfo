using System.Text.Json;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;

namespace CfoAgent.Api.Agents.Configuration;

public static class AgentPromptTemplates
{
    public static string ForSalesSummary(SalesSummary summary) => Create("[MOCK:SALES_SUMMARY]", summary);

    public static string ForSalesComparison(WeeklySalesComparison comparison) => Create("[MOCK:SALES_COMPARISON]", comparison);

    public static string ForTopProducts(TopProductsResult topProducts) => Create("[MOCK:TOP_PRODUCTS]", topProducts);

    public static string ForForecast(SalesForecastResult forecast) => Create("[MOCK:FORECAST]", forecast);

    public static string ForKnowledge(string retrievedContext) => $"[MOCK:KNOWLEDGE]\n{retrievedContext}";

    private static string Create(string marker, object verifiedPayload) => $"{marker}\n{JsonSerializer.Serialize(verifiedPayload)}";
}
