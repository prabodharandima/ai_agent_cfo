using CfoAgent.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CfoAgent.Api.Features.Sales;

public sealed class SalesAnalysisService(FinanceDbContext dbContext, TimeProvider timeProvider)
{
    /// <summary>
    /// Uses Monday through the injected local current date for the current week.
    /// Week-over-week comparisons use the preceding full Monday-through-Sunday week.
    /// </summary>
    public async Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken)
    {
        var currentDate = GetCurrentDate();
        var currentWeekStart = StartOfWeek(currentDate);

        return await GetSummaryAsync(new SalesPeriod(currentWeekStart, currentDate), cancellationToken);
    }

    public async Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken)
    {
        var currentDate = GetCurrentDate();
        var currentWeekStart = StartOfWeek(currentDate);
        var currentWeek = await GetSummaryAsync(new SalesPeriod(currentWeekStart, currentDate), cancellationToken);
        var previousWeek = await GetSummaryAsync(
            new SalesPeriod(currentWeekStart.AddDays(-7), currentWeekStart.AddDays(-1)),
            cancellationToken);

        var change = currentWeek.NetRevenue - previousWeek.NetRevenue;
        var warnings = new List<string>();
        decimal? percentageChange = null;

        if (previousWeek.NetRevenue == 0m)
        {
            if (currentWeek.NetRevenue == 0m)
            {
                percentageChange = 0m;
            }
            else
            {
                warnings.Add("The previous week has no net revenue, so a percentage change is unavailable.");
            }
        }
        else
        {
            percentageChange = change / previousWeek.NetRevenue * 100m;
        }

        var direction = change switch
        {
            > 0m => SalesChangeDirection.Increased,
            < 0m => SalesChangeDirection.Decreased,
            _ => SalesChangeDirection.Unchanged
        };

        return new WeeklySalesComparison(currentWeek, previousWeek, change, percentageChange, direction, warnings);
    }

    public async Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken)
    {
        var currentDate = GetCurrentDate();
        var period = new SalesPeriod(new DateOnly(currentDate.Year, currentDate.Month, 1), currentDate);
        var sales = await GetSalesAsync(period, cancellationToken);
        var products = sales
            .GroupBy(sale => new { sale.ProductCode, sale.ProductName })
            .Select(group => new TopProduct(
                group.Key.ProductCode,
                group.Key.ProductName,
                group.Sum(sale => sale.Quantity),
                group.Sum(NetRevenue),
                group.Sum(GrossProfit)))
            .OrderByDescending(product => product.NetRevenue)
            .ThenBy(product => product.ProductCode, StringComparer.Ordinal)
            .Take(5)
            .ToArray();

        var warnings = products.Length == 0
            ? new[] { "No sales data is available for the current month." }
            : Array.Empty<string>();

        return new TopProductsResult(period, products, warnings);
    }

    public async Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken)
    {
        var firstCurrentYearDay = new DateOnly(GetCurrentDate().Year, 1, 1);
        var sales = await dbContext.Sales
            .AsNoTracking()
            .Where(sale => sale.SaleDate < firstCurrentYearDay)
            .Select(sale => new HistoricalSale(sale.SaleDate, sale.Quantity, sale.UnitPrice, sale.DiscountAmount))
            .ToListAsync(cancellationToken);
        var totals = sales
            .GroupBy(sale => sale.SaleDate.Year)
            .Select(group => new YearlySalesTotal(group.Key, group.Sum(NetRevenue)))
            .OrderBy(total => total.Year)
            .ToArray();
        var warnings = totals.Length == 0
            ? new[] { "No complete historical sales years are available." }
            : Array.Empty<string>();

        return new HistoricalYearlySalesResult(totals, warnings);
    }

    public async Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken)
    {
        if (year <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        var target = await dbContext.BudgetTargets
            .AsNoTracking()
            .SingleOrDefaultAsync(target => target.Year == year && target.Month == month, cancellationToken);

        if (target is null)
        {
            return new BudgetTargetResult(
                year,
                month,
                false,
                null,
                null,
                null,
                ["No budget target is available for the requested period."]);
        }

        return new BudgetTargetResult(
            target.Year,
            target.Month,
            true,
            target.SalesTarget,
            target.ProfitTarget,
            target.AssumptionReference,
            Array.Empty<string>());
    }

    private async Task<SalesSummary> GetSummaryAsync(SalesPeriod period, CancellationToken cancellationToken)
    {
        var sales = await GetSalesAsync(period, cancellationToken);
        var netRevenue = sales.Sum(NetRevenue);
        var costOfGoodsSold = sales.Sum(CostOfGoodsSold);
        var grossProfit = netRevenue - costOfGoodsSold;
        var orderCount = sales.Select(sale => sale.OrderNumber).Distinct(StringComparer.Ordinal).Count();
        var topProduct = sales
            .GroupBy(sale => new { sale.ProductCode, sale.ProductName })
            .Select(group => new TopProduct(
                group.Key.ProductCode,
                group.Key.ProductName,
                group.Sum(sale => sale.Quantity),
                group.Sum(NetRevenue),
                group.Sum(GrossProfit)))
            .OrderByDescending(product => product.NetRevenue)
            .ThenBy(product => product.ProductCode, StringComparer.Ordinal)
            .FirstOrDefault();
        var warnings = new List<string>();

        if (sales.Count == 0)
        {
            warnings.Add("No sales data is available for this period.");
        }

        if (netRevenue == 0m)
        {
            warnings.Add("Gross margin percentage is unavailable because net revenue is zero.");
        }

        return new SalesSummary(
            period,
            netRevenue,
            costOfGoodsSold,
            grossProfit,
            netRevenue == 0m ? 0m : grossProfit / netRevenue * 100m,
            sales.Sum(sale => sale.Quantity),
            orderCount,
            orderCount == 0 ? 0m : netRevenue / orderCount,
            topProduct,
            warnings);
    }

    private async Task<List<SaleLine>> GetSalesAsync(SalesPeriod period, CancellationToken cancellationToken)
    {
        return await dbContext.Sales
            .AsNoTracking()
            .Where(sale => sale.SaleDate >= period.StartDate && sale.SaleDate <= period.EndDate)
            .Select(sale => new SaleLine(
                sale.OrderNumber,
                sale.Product.Code,
                sale.Product.Name,
                sale.Quantity,
                sale.UnitPrice,
                sale.DiscountAmount,
                sale.UnitCost))
            .ToListAsync(cancellationToken);
    }

    private DateOnly GetCurrentDate() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);

    private static decimal NetRevenue(SaleLine sale) => sale.Quantity * sale.UnitPrice - sale.DiscountAmount;

    private static decimal CostOfGoodsSold(SaleLine sale) => sale.Quantity * sale.UnitCost;

    private static decimal GrossProfit(SaleLine sale) => NetRevenue(sale) - CostOfGoodsSold(sale);

    private static decimal NetRevenue(HistoricalSale sale) => sale.Quantity * sale.UnitPrice - sale.DiscountAmount;

    private sealed record SaleLine(
        string OrderNumber,
        string ProductCode,
        string ProductName,
        decimal Quantity,
        decimal UnitPrice,
        decimal DiscountAmount,
        decimal UnitCost);

    private sealed record HistoricalSale(DateOnly SaleDate, decimal Quantity, decimal UnitPrice, decimal DiscountAmount);
}
