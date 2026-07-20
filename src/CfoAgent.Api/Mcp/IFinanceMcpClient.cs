using CfoAgent.Api.Features.Sales;

namespace CfoAgent.Api.Mcp;

public interface IFinanceMcpClient
{
    Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken);

    Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken);

    Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken);

    Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken);

    Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken);
}
