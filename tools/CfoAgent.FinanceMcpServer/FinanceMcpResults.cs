namespace CfoAgent.FinanceMcpServer;

public sealed record FinanceMcpResult<T>(bool IsSuccess, T? Data, string? Error)
{
    public static FinanceMcpResult<T> Success(T data) => new(true, data, null);

    public static FinanceMcpResult<T> Failure(string error) => new(false, default, error);
}

public sealed record McpSalesPeriod(string StartDate, string EndDate);

public sealed record McpTopProduct(string ProductCode, string ProductName, decimal QuantitySold, decimal NetRevenue, decimal GrossProfit);

public sealed record McpSalesSummary(
    McpSalesPeriod Period,
    decimal NetRevenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit,
    decimal GrossMarginPercent,
    decimal QuantitySold,
    int OrderCount,
    decimal AverageOrderValue,
    McpTopProduct? TopProduct,
    IReadOnlyList<string> Warnings);

public sealed record McpPeriodComparison(
    McpSalesSummary CurrentPeriod,
    McpSalesSummary PreviousPeriod,
    decimal NetRevenueChange,
    decimal? NetRevenueChangePercentage,
    string Direction,
    IReadOnlyList<string> Warnings);

public sealed record McpTopProducts(McpSalesPeriod Period, IReadOnlyList<McpTopProduct> Products, IReadOnlyList<string> Warnings);

public sealed record McpYearlySalesTotal(int Year, decimal NetRevenue);

public sealed record McpHistoricalSales(IReadOnlyList<McpYearlySalesTotal> Totals, IReadOnlyList<string> Warnings);

public sealed record McpBudgetTarget(
    int Year,
    int? Month,
    bool IsAvailable,
    decimal? SalesTarget,
    decimal? ProfitTarget,
    string? AssumptionReference,
    IReadOnlyList<string> Warnings);
