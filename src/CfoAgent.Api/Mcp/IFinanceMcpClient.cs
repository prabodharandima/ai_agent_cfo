using CfoAgent.Api.Features.Sales;

namespace CfoAgent.Api.Mcp;

public interface IFinanceMcpClient
{
    Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken);

    Task<SalesSummary> GetCurrentWeekSummaryAsync(string userMessage, CancellationToken cancellationToken) =>
        GetCurrentWeekSummaryAsync(cancellationToken);

    Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken);

    Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(string userMessage, CancellationToken cancellationToken) =>
        GetWeekOverWeekComparisonAsync(cancellationToken);

    Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken);

    Task<TopProductsResult> GetCurrentMonthTopProductsAsync(string userMessage, CancellationToken cancellationToken) =>
        GetCurrentMonthTopProductsAsync(cancellationToken);

    Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken);

    Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(string userMessage, CancellationToken cancellationToken) =>
        GetHistoricalYearlyTotalsAsync(cancellationToken);

    Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken);
}
