namespace CfoAgent.Api.Features.Sales;

public sealed record SalesPeriod(DateOnly StartDate, DateOnly EndDate);

public sealed record TopProduct(
    string ProductCode,
    string ProductName,
    decimal QuantitySold,
    decimal NetRevenue,
    decimal GrossProfit);

public sealed record SalesSummary(
    SalesPeriod Period,
    decimal NetRevenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit,
    decimal GrossMarginPercent,
    decimal QuantitySold,
    int OrderCount,
    decimal AverageOrderValue,
    TopProduct? TopProduct,
    IReadOnlyList<string> Warnings);

public enum SalesChangeDirection
{
    Unchanged,
    Increased,
    Decreased
}

public sealed record WeeklySalesComparison(
    SalesSummary CurrentWeek,
    SalesSummary PreviousWeek,
    decimal NetRevenueChange,
    decimal? NetRevenueChangePercentage,
    SalesChangeDirection Direction,
    IReadOnlyList<string> Warnings);

public sealed record TopProductsResult(
    SalesPeriod Period,
    IReadOnlyList<TopProduct> Products,
    IReadOnlyList<string> Warnings);

public sealed record YearlySalesTotal(int Year, decimal NetRevenue);

public sealed record HistoricalYearlySalesResult(
    IReadOnlyList<YearlySalesTotal> Totals,
    IReadOnlyList<string> Warnings);

public sealed record BudgetTargetResult(
    int Year,
    int? Month,
    bool IsAvailable,
    decimal? SalesTarget,
    decimal? ProfitTarget,
    string? AssumptionReference,
    IReadOnlyList<string> Warnings);
