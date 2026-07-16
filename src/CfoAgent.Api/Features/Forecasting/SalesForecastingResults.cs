using CfoAgent.Api.Features.Sales;

namespace CfoAgent.Api.Features.Forecasting;

public sealed record SalesForecastRow(
    int Year,
    decimal ConservativeNetRevenue,
    decimal ExpectedNetRevenue,
    decimal OptimisticNetRevenue);

public sealed record SalesForecastResult(
    string MethodName,
    int? HistoricalPeriodStartYear,
    int? HistoricalPeriodEndYear,
    IReadOnlyList<YearlySalesTotal> HistoricalInputs,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<SalesForecastRow> Forecasts,
    IReadOnlyList<string> Warnings);
