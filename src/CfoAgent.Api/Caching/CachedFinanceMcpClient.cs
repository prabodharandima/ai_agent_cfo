using System.Globalization;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Caching;

public sealed class CachedFinanceMcpClient(
    IFinanceMcpClient innerClient,
    IApplicationCache cache,
    IOptions<CacheOptions> options,
    TimeProvider timeProvider) : IFinanceMcpClient
{
    private const string KeyPrefix = "finance:v1";
    private readonly FinanceCacheOptions financeOptions = options.Value.Finance;

    public Task<SalesSummary> GetSalesSummaryAsync(SalesPeriod period, CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"{KeyPrefix}:sales-summary:{FormatDate(period.StartDate)}:{FormatDate(period.EndDate)}",
            financeOptions.SalesSummaryTtlSeconds,
            token => innerClient.GetSalesSummaryAsync(period, token),
            cancellationToken);

    public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"{KeyPrefix}:current-week-summary:{FormatDate(GetCurrentDate())}",
            financeOptions.CurrentWeekSummaryTtlSeconds,
            innerClient.GetCurrentWeekSummaryAsync,
            cancellationToken);

    public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"{KeyPrefix}:week-over-week-comparison:{FormatDate(GetCurrentDate())}",
            financeOptions.WeekOverWeekComparisonTtlSeconds,
            innerClient.GetWeekOverWeekComparisonAsync,
            cancellationToken);

    public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"{KeyPrefix}:current-month-top-products:{FormatDate(GetCurrentDate())}",
            financeOptions.CurrentMonthTopProductsTtlSeconds,
            innerClient.GetCurrentMonthTopProductsAsync,
            cancellationToken);

    public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"{KeyPrefix}:historical-yearly-totals:{GetCurrentDate().Year.ToString(CultureInfo.InvariantCulture)}",
            financeOptions.HistoricalYearlyTotalsTtlSeconds,
            innerClient.GetHistoricalYearlyTotalsAsync,
            cancellationToken);

    public Task<BudgetTargetResult> GetBudgetTargetAsync(
        int year,
        int? month,
        CancellationToken cancellationToken) =>
        GetOrCreateAsync(
            $"{KeyPrefix}:budget-target:{year.ToString(CultureInfo.InvariantCulture)}:{month?.ToString("D2", CultureInfo.InvariantCulture) ?? "annual"}",
            financeOptions.BudgetTargetTtlSeconds,
            token => innerClient.GetBudgetTargetAsync(year, month, token),
            cancellationToken);

    private Task<T> GetOrCreateAsync<T>(
        string key,
        int timeToLiveSeconds,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(key, TimeSpan.FromSeconds(timeToLiveSeconds), factory, cancellationToken);

    private DateOnly GetCurrentDate() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private static string FormatDate(DateOnly date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
}
