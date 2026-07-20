using System.ComponentModel;
using CfoAgent.FinanceMcpServer.Data;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace CfoAgent.FinanceMcpServer;

[McpServerToolType]
public sealed class FinanceMcpTools(FinanceDbContext dbContext)
{
    private const int MaximumPeriodDays = 366;
    private const int MaximumTopProducts = 20;
    private const int MaximumHistoricalYears = 5;

    [McpServerTool(Name = "get_sales_summary")]
    [Description("Returns a read-only sales summary for a date range up to 366 days.")]
    public async Task<FinanceMcpResult<McpSalesSummary>> GetSalesSummaryAsync(
        [Description("Inclusive start date in YYYY-MM-DD format.")] string startDate,
        [Description("Inclusive end date in YYYY-MM-DD format.")] string endDate,
        CancellationToken cancellationToken)
    {
        if (!TryGetPeriod(startDate, endDate, out var start, out var end, out var error))
        {
            return FinanceMcpResult<McpSalesSummary>.Failure(error);
        }

        return FinanceMcpResult<McpSalesSummary>.Success(await BuildSummaryAsync(start, end, cancellationToken));
    }

    [McpServerTool(Name = "compare_sales_periods")]
    [Description("Compares two read-only sales periods, each no longer than 366 days.")]
    public async Task<FinanceMcpResult<McpPeriodComparison>> CompareSalesPeriodsAsync(
        string currentStartDate,
        string currentEndDate,
        string previousStartDate,
        string previousEndDate,
        CancellationToken cancellationToken)
    {
        if (!TryGetPeriod(currentStartDate, currentEndDate, out var currentStart, out var currentEnd, out var error) ||
            !TryGetPeriod(previousStartDate, previousEndDate, out var previousStart, out var previousEnd, out error))
        {
            return FinanceMcpResult<McpPeriodComparison>.Failure(error);
        }

        var current = await BuildSummaryAsync(currentStart, currentEnd, cancellationToken);
        var previous = await BuildSummaryAsync(previousStart, previousEnd, cancellationToken);
        var change = current.NetRevenue - previous.NetRevenue;
        var warnings = new List<string>();
        decimal? percentage = previous.NetRevenue == 0m ? null : change / previous.NetRevenue * 100m;
        if (percentage is null)
        {
            warnings.Add("The previous period has no net revenue, so a percentage change is unavailable.");
        }

        var direction = change switch { > 0m => "increased", < 0m => "decreased", _ => "unchanged" };
        return FinanceMcpResult<McpPeriodComparison>.Success(new(current, previous, change, percentage, direction, warnings));
    }

    [McpServerTool(Name = "get_top_products")]
    [Description("Returns up to 20 top products by net revenue for a date range up to 366 days.")]
    public async Task<FinanceMcpResult<McpTopProducts>> GetTopProductsAsync(
        string startDate,
        string endDate,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetPeriod(startDate, endDate, out var start, out var end, out var error))
        {
            return FinanceMcpResult<McpTopProducts>.Failure(error);
        }

        if (limit is < 1 or > MaximumTopProducts)
        {
            return FinanceMcpResult<McpTopProducts>.Failure($"limit must be between 1 and {MaximumTopProducts}.");
        }

        var sales = await dbContext.Sales.AsNoTracking()
            .Where(sale => sale.SaleDate >= start && sale.SaleDate <= end)
            .Select(sale => new SaleLine(
                sale.OrderNumber,
                sale.Product.Code,
                sale.Product.Name,
                sale.SaleDate,
                sale.Quantity,
                sale.UnitPrice,
                sale.DiscountAmount,
                sale.UnitCost))
            .ToListAsync(cancellationToken);
        var products = sales
            .GroupBy(sale => new { sale.ProductCode, sale.ProductName })
            .Select(group => new McpTopProduct(
                group.Key.ProductCode,
                group.Key.ProductName,
                group.Sum(sale => sale.Quantity),
                group.Sum(sale => sale.Quantity * sale.UnitPrice - sale.DiscountAmount),
                group.Sum(sale => sale.Quantity * sale.UnitPrice - sale.DiscountAmount - sale.Quantity * sale.UnitCost)))
            .OrderByDescending(product => product.NetRevenue)
            .ThenBy(product => product.ProductCode, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();

        var warnings = products.Length == 0 ? new[] { "No sales data is available for this period." } : Array.Empty<string>();
        return FinanceMcpResult<McpTopProducts>.Success(new(new(start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd")), products, warnings));
    }

    [McpServerTool(Name = "get_historical_sales")]
    [Description("Returns yearly net sales totals for up to five complete calendar years.")]
    public async Task<FinanceMcpResult<McpHistoricalSales>> GetHistoricalSalesAsync(int startYear, int endYear, CancellationToken cancellationToken)
    {
        if (startYear is < 2000 or > 2100 || endYear is < 2000 or > 2100 || endYear < startYear || endYear - startYear >= MaximumHistoricalYears)
        {
            return FinanceMcpResult<McpHistoricalSales>.Failure("Provide an inclusive year range from 2000 through 2100 containing at most five years.");
        }

        var sales = await dbContext.Sales.AsNoTracking()
            .Where(sale => sale.SaleDate.Year >= startYear && sale.SaleDate.Year <= endYear)
            .Select(sale => new SaleLine(
                sale.OrderNumber,
                sale.Product.Code,
                sale.Product.Name,
                sale.SaleDate,
                sale.Quantity,
                sale.UnitPrice,
                sale.DiscountAmount,
                sale.UnitCost))
            .ToListAsync(cancellationToken);
        var totals = sales
            .GroupBy(sale => sale.SaleDate.Year)
            .Select(group => new McpYearlySalesTotal(group.Key, group.Sum(sale => sale.Quantity * sale.UnitPrice - sale.DiscountAmount)))
            .OrderBy(total => total.Year)
            .ToArray();
        var warnings = totals.Length == 0 ? new[] { "No historical sales data is available for the requested years." } : Array.Empty<string>();
        return FinanceMcpResult<McpHistoricalSales>.Success(new(totals, warnings));
    }

    [McpServerTool(Name = "get_budget_target")]
    [Description("Returns a read-only annual or monthly budget target.")]
    public async Task<FinanceMcpResult<McpBudgetTarget>> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 2100 || month is < 1 or > 12)
        {
            return FinanceMcpResult<McpBudgetTarget>.Failure("year must be from 2000 through 2100 and month, when provided, must be from 1 through 12.");
        }

        var target = await dbContext.BudgetTargets.AsNoTracking()
            .SingleOrDefaultAsync(target => target.Year == year && target.Month == month, cancellationToken);
        if (target is null)
        {
            return FinanceMcpResult<McpBudgetTarget>.Success(new(year, month, false, null, null, null, ["No budget target is available for the requested period."]));
        }

        return FinanceMcpResult<McpBudgetTarget>.Success(new(target.Year, target.Month, true, target.SalesTarget, target.ProfitTarget, target.AssumptionReference, Array.Empty<string>()));
    }

    private async Task<McpSalesSummary> BuildSummaryAsync(DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var sales = await dbContext.Sales.AsNoTracking()
            .Where(sale => sale.SaleDate >= start && sale.SaleDate <= end)
            .Select(sale => new SaleLine(
                sale.OrderNumber,
                sale.Product.Code,
                sale.Product.Name,
                sale.SaleDate,
                sale.Quantity,
                sale.UnitPrice,
                sale.DiscountAmount,
                sale.UnitCost)
            )
            .ToListAsync(cancellationToken);

        var revenue = sales.Sum(sale => sale.Quantity * sale.UnitPrice - sale.DiscountAmount);

        var cost = sales.Sum(sale => sale.Quantity * sale.UnitCost);

        var orders = sales.Select(sale => sale.OrderNumber).Distinct(StringComparer.Ordinal).Count();

        var top = sales.GroupBy(sale => new { sale.ProductCode, sale.ProductName })
            .Select(group => new McpTopProduct(group.Key.ProductCode, group.Key.ProductName, group.Sum(sale => sale.Quantity), group.Sum(sale => sale.Quantity * sale.UnitPrice - sale.DiscountAmount), group.Sum(sale => sale.Quantity * sale.UnitPrice - sale.DiscountAmount - sale.Quantity * sale.UnitCost)))
            .OrderByDescending(product => product.NetRevenue).ThenBy(product => product.ProductCode, StringComparer.Ordinal).FirstOrDefault();

        var warnings = new List<string>();
        if (sales.Count == 0)
            warnings.Add("No sales data is available for this period.");
        if (revenue == 0m)
            warnings.Add("Gross margin percentage is unavailable because net revenue is zero.");

        return new(
            new(start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd")),
            revenue,
            cost,
            revenue - cost,
            revenue == 0m ? 0m : (revenue - cost) / revenue * 100m,
            sales.Sum(sale => sale.Quantity),
            orders,
            orders == 0 ? 0m : revenue / orders,
            top,
            warnings
        );
    }

    private static bool TryGetPeriod(string startText, string endText, out DateOnly start, out DateOnly end, out string error)
    {
        start = default;
        end = default;
        if (!DateOnly.TryParseExact(startText, "yyyy-MM-dd", out start) || !DateOnly.TryParseExact(endText, "yyyy-MM-dd", out end))
        {
            error = "Dates must use YYYY-MM-DD format.";
            return false;
        }

        if (end < start || end.DayNumber - start.DayNumber + 1 > MaximumPeriodDays)
        {
            error = $"Date ranges must be ordered and contain no more than {MaximumPeriodDays} days.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private sealed record SaleLine(string OrderNumber, string ProductCode, string ProductName, DateOnly SaleDate, decimal Quantity, decimal UnitPrice, decimal DiscountAmount, decimal UnitCost);
}
