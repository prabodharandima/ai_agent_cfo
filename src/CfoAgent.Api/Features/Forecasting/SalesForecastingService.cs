using CfoAgent.Api.Features.Sales;

namespace CfoAgent.Api.Features.Forecasting;

public sealed class SalesForecastingService(SalesAnalysisService salesAnalysisService, TimeProvider timeProvider)
{
    private const int MinimumHistoricalYears = 3;
    private const int ForecastYears = 5;
    private const decimal ScenarioAdjustment = 0.10m;

    public async Task<SalesForecastResult> ForecastAsync(CancellationToken cancellationToken)
    {
        var historical = await salesAnalysisService.GetHistoricalYearlyTotalsAsync(cancellationToken);
        return Forecast(historical);
    }

    public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken) =>
        salesAnalysisService.GetHistoricalYearlyTotalsAsync(cancellationToken);

    public SalesForecastResult Forecast(HistoricalYearlySalesResult historical)
    {
        ArgumentNullException.ThrowIfNull(historical);
        var assumptions = new[]
        {
            "Expected net revenue uses ordinary least-squares linear regression over complete calendar-year net revenue totals.",
            "Conservative and optimistic scenarios apply -10% and +10% to the expected forecast.",
            "Forecast values are deterministic planning estimates and do not include an LLM."
        };

        if (historical.Totals.Count < MinimumHistoricalYears)
        {
            return new SalesForecastResult(
                "Ordinary least-squares linear regression",
                historical.Totals.FirstOrDefault()?.Year,
                historical.Totals.LastOrDefault()?.Year,
                historical.Totals,
                assumptions,
                Array.Empty<SalesForecastRow>(),
                ["At least three complete historical sales years are required to produce a forecast."]);
        }

        var (intercept, slope) = CalculateRegression(historical.Totals);
        var firstForecastYear = timeProvider.GetLocalNow().Year;
        var forecasts = Enumerable.Range(0, ForecastYears)
            .Select(offset =>
            {
                var expected = Math.Max(0m, intercept + slope * (historical.Totals.Count + offset));
                return new SalesForecastRow(
                    firstForecastYear + offset,
                    expected * (1m - ScenarioAdjustment),
                    expected,
                    expected * (1m + ScenarioAdjustment));
            })
            .ToArray();

        return new SalesForecastResult(
            "Ordinary least-squares linear regression",
            historical.Totals[0].Year,
            historical.Totals[^1].Year,
            historical.Totals,
            assumptions,
            forecasts,
            Array.Empty<string>());
    }

    private static (decimal Intercept, decimal Slope) CalculateRegression(IReadOnlyList<YearlySalesTotal> totals)
    {
        var count = totals.Count;
        var sumX = 0m;
        var sumY = 0m;
        var sumXy = 0m;
        var sumX2 = 0m;

        for (var index = 0; index < count; index++)
        {
            var x = (decimal)index;
            var y = totals[index].NetRevenue;
            sumX += x;
            sumY += y;
            sumXy += x * y;
            sumX2 += x * x;
        }

        var denominator = count * sumX2 - sumX * sumX;
        var slope = denominator == 0m ? 0m : (count * sumXy - sumX * sumY) / denominator;
        var intercept = (sumY - slope * sumX) / count;

        return (intercept, slope);
    }
}
