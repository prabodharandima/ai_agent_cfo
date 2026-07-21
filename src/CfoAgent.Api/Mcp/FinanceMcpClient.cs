using System.Globalization;
using System.Text.Json;
using CfoAgent.Api.Features.Sales;
using Microsoft.Extensions.DependencyInjection;

namespace CfoAgent.Api.Mcp;

public sealed class FinanceMcpClient(
    [FromKeyedServices(McpToolAdapter.FinanceKey)] IMcpToolAdapter toolAdapter,
    TimeProvider timeProvider,
    ILogger<FinanceMcpClient> logger) : IFinanceMcpRemoteClient
{
    private const string DependencyName = "Finance MCP";
    private static readonly string[] RequiredTools =
        ["get_sales_summary", "compare_sales_periods", "get_top_products", "get_historical_sales", "get_budget_target"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken)
    {
        var currentDate = GetCurrentDate();
        return await GetSalesSummaryAsync(new SalesPeriod(StartOfWeek(currentDate), currentDate), cancellationToken);
    }

    public async Task<SalesSummary> GetSalesSummaryAsync(SalesPeriod period, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        if (period.EndDate < period.StartDate)
        {
            throw new ArgumentException("The sales summary end date must not be earlier than the start date.", nameof(period));
        }

        var result = await CallAndMapAsync<McpSalesSummary>(
            "get_sales_summary",
            new Dictionary<string, object?>
            {
                ["startDate"] = FormatDate(period.StartDate),
                ["endDate"] = FormatDate(period.EndDate)
            },
            cancellationToken);

        return ToSalesSummary(result);
    }

    public async Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken)
    {
        var currentDate = GetCurrentDate();
        var currentStart = StartOfWeek(currentDate);
        var result = await CallAndMapAsync<McpPeriodComparison>(
            "compare_sales_periods",
            new Dictionary<string, object?>
            {
                ["currentStartDate"] = FormatDate(currentStart),
                ["currentEndDate"] = FormatDate(currentDate),
                ["previousStartDate"] = FormatDate(currentStart.AddDays(-7)),
                ["previousEndDate"] = FormatDate(currentStart.AddDays(-1))
            },
            cancellationToken);

        return new WeeklySalesComparison(
            ToSalesSummary(result.CurrentPeriod),
            ToSalesSummary(result.PreviousPeriod),
            result.NetRevenueChange,
            result.NetRevenueChangePercentage,
            ParseDirection(result.Direction),
            result.Warnings);
    }

    public async Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken)
    {
        var currentDate = GetCurrentDate();
        var result = await CallAndMapAsync<McpTopProducts>(
            "get_top_products",
            new Dictionary<string, object?>
            {
                ["startDate"] = FormatDate(new DateOnly(currentDate.Year, currentDate.Month, 1)),
                ["endDate"] = FormatDate(currentDate),
                ["limit"] = 5
            },
            cancellationToken);

        return new TopProductsResult(ToSalesPeriod(result.Period), result.Products.Select(ToTopProduct).ToArray(), result.Warnings);
    }

    public async Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken)
    {
        var endYear = GetCurrentDate().Year - 1;
        var result = await CallAndMapAsync<McpHistoricalSales>(
            "get_historical_sales",
            new Dictionary<string, object?>
            {
                ["startYear"] = endYear - 4,
                ["endYear"] = endYear
            },
            cancellationToken);

        return new HistoricalYearlySalesResult(
            result.Totals.Select(total => new YearlySalesTotal(total.Year, total.NetRevenue)).ToArray(),
            result.Warnings);
    }

    public async Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken)
    {
        var result = await CallAndMapAsync<McpBudgetTarget>(
            "get_budget_target",
            new Dictionary<string, object?> { ["year"] = year, ["month"] = month },
            cancellationToken);
        return new BudgetTargetResult(
            result.Year,
            result.Month,
            result.IsAvailable,
            result.SalesTarget,
            result.ProfitTarget,
            result.AssumptionReference,
            result.Warnings);
    }

    public async Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken)
    {
        var tools = await toolAdapter.GetApprovedToolNamesAsync(RequiredTools, cancellationToken);
        return tools.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private async Task<T> CallAndMapAsync<T>(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var data = await toolAdapter.CallApprovedToolAsync(toolName, arguments, cancellationToken);
        try
        {
            var result = data.Deserialize<T>(JsonOptions);
            if (result is null)
            {
                throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse);
            }

            logger.LogInformation("Finance MCP tool {ToolName} mapped to the existing finance result contract.", toolName);
            return result;
        }
        catch (JsonException exception)
        {
            throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse, exception);
        }
    }

    private DateOnly GetCurrentDate() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static SalesSummary ToSalesSummary(McpSalesSummary summary) => new(
        ToSalesPeriod(summary.Period),
        summary.NetRevenue,
        summary.CostOfGoodsSold,
        summary.GrossProfit,
        summary.GrossMarginPercent,
        summary.QuantitySold,
        summary.OrderCount,
        summary.AverageOrderValue,
        summary.TopProduct is null ? null : ToTopProduct(summary.TopProduct),
        summary.Warnings);

    private static SalesPeriod ToSalesPeriod(McpSalesPeriod period) => new(
        DateOnly.ParseExact(period.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateOnly.ParseExact(period.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static TopProduct ToTopProduct(McpTopProduct product) => new(
        product.ProductCode,
        product.ProductName,
        product.QuantitySold,
        product.NetRevenue,
        product.GrossProfit);

    private static SalesChangeDirection ParseDirection(string direction) => direction.ToLowerInvariant() switch
    {
        "increased" => SalesChangeDirection.Increased,
        "decreased" => SalesChangeDirection.Decreased,
        "unchanged" => SalesChangeDirection.Unchanged,
        _ => throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse)
    };

    private sealed record McpSalesPeriod(string StartDate, string EndDate);
    private sealed record McpTopProduct(string ProductCode, string ProductName, decimal QuantitySold, decimal NetRevenue, decimal GrossProfit);
    private sealed record McpSalesSummary(McpSalesPeriod Period, decimal NetRevenue, decimal CostOfGoodsSold, decimal GrossProfit, decimal GrossMarginPercent, decimal QuantitySold, int OrderCount, decimal AverageOrderValue, McpTopProduct? TopProduct, IReadOnlyList<string> Warnings);
    private sealed record McpPeriodComparison(McpSalesSummary CurrentPeriod, McpSalesSummary PreviousPeriod, decimal NetRevenueChange, decimal? NetRevenueChangePercentage, string Direction, IReadOnlyList<string> Warnings);
    private sealed record McpTopProducts(McpSalesPeriod Period, IReadOnlyList<McpTopProduct> Products, IReadOnlyList<string> Warnings);
    private sealed record McpYearlySalesTotal(int Year, decimal NetRevenue);
    private sealed record McpHistoricalSales(IReadOnlyList<McpYearlySalesTotal> Totals, IReadOnlyList<string> Warnings);
    private sealed record McpBudgetTarget(int Year, int? Month, bool IsAvailable, decimal? SalesTarget, decimal? ProfitTarget, string? AssumptionReference, IReadOnlyList<string> Warnings);
}
