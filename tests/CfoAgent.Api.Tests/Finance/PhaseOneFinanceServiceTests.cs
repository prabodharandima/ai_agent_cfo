using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;

namespace CfoAgent.Api.Tests.Finance;

public class PhaseOneFinanceServiceTests
{
    private static readonly DateOnly DemoDate = new(2026, 7, 15);
    private static readonly TimeProvider Clock = new FixedTimeProvider(DemoDate);

    [Fact]
    public async Task CurrentWeekSummaryUsesMondayBoundaryAndCalculatesKpis()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var alpha = await database.AddProductAsync("ALPHA", "Alpha");
        var beta = await database.AddProductAsync("BETA", "Beta");

        database.AddSale(alpha, "ORDER-001", new DateOnly(2026, 7, 13), 2, 100m, 10m, 40m);
        database.AddSale(beta, "ORDER-001", new DateOnly(2026, 7, 13), 1, 50m, 0m, 20m);
        database.AddSale(beta, "ORDER-002", DemoDate, 4, 50m, 0m, 20m);
        database.AddSale(alpha, "OUTSIDE-WEEK", new DateOnly(2026, 7, 12), 10, 100m, 0m, 40m);
        await database.SaveChangesAsync();

        var result = await new SalesAnalysisService(database.Context, Clock).GetCurrentWeekSummaryAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 13), result.Period.StartDate);
        Assert.Equal(DemoDate, result.Period.EndDate);
        Assert.Equal(440m, result.NetRevenue);
        Assert.Equal(180m, result.CostOfGoodsSold);
        Assert.Equal(260m, result.GrossProfit);
        Assert.Equal(260m / 440m * 100m, result.GrossMarginPercent);
        Assert.Equal(7m, result.QuantitySold);
        Assert.Equal(2, result.OrderCount);
        Assert.Equal(220m, result.AverageOrderValue);
        Assert.NotNull(result.TopProduct);
        Assert.Equal("BETA", result.TopProduct.ProductCode);
        Assert.Equal(250m, result.TopProduct.NetRevenue);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task WeekOverWeekComparisonCalculatesDirectionAndPercent()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var product = await database.AddProductAsync("SALES", "Sales");
        database.AddSale(product, "PREVIOUS", new DateOnly(2026, 7, 8), 1, 100m, 0m, 40m);
        database.AddSale(product, "CURRENT", new DateOnly(2026, 7, 14), 1, 150m, 0m, 60m);
        await database.SaveChangesAsync();

        var result = await new SalesAnalysisService(database.Context, Clock).GetWeekOverWeekComparisonAsync(CancellationToken.None);

        Assert.Equal(100m, result.PreviousWeek.NetRevenue);
        Assert.Equal(150m, result.CurrentWeek.NetRevenue);
        Assert.Equal(50m, result.NetRevenueChange);
        Assert.Equal(50m, result.NetRevenueChangePercentage);
        Assert.Equal(SalesChangeDirection.Increased, result.Direction);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task WeekOverWeekComparisonMarksPositiveRevenueAfterZeroWeekAsUnavailable()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var product = await database.AddProductAsync("SALES", "Sales");
        database.AddSale(product, "CURRENT", new DateOnly(2026, 7, 14), 1, 150m, 0m, 60m);
        await database.SaveChangesAsync();

        var result = await new SalesAnalysisService(database.Context, Clock).GetWeekOverWeekComparisonAsync(CancellationToken.None);

        Assert.Null(result.NetRevenueChangePercentage);
        Assert.Equal(SalesChangeDirection.Increased, result.Direction);
        Assert.Contains(result.Warnings, warning => warning.Contains("percentage change", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CurrentMonthTopProductsRanksByRevenueThenProductCodeAndLimitsToFive()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var products = new[]
        {
            await database.AddProductAsync("BETA", "Beta"),
            await database.AddProductAsync("ALPHA", "Alpha"),
            await database.AddProductAsync("GAMMA", "Gamma"),
            await database.AddProductAsync("DELTA", "Delta"),
            await database.AddProductAsync("EPSILON", "Epsilon"),
            await database.AddProductAsync("ZETA", "Zeta")
        };
        var revenues = new[] { 100m, 100m, 90m, 80m, 70m, 60m };

        for (var index = 0; index < products.Length; index++)
        {
            database.AddSale(products[index], $"ORDER-{index}", DemoDate, 1, revenues[index], 0m, 10m);
        }

        database.AddSale(products[0], "PRIOR-MONTH", new DateOnly(2026, 6, 30), 1, 1_000m, 0m, 10m);
        await database.SaveChangesAsync();

        var result = await new SalesAnalysisService(database.Context, Clock).GetCurrentMonthTopProductsAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 1), result.Period.StartDate);
        Assert.Equal(DemoDate, result.Period.EndDate);
        Assert.Equal(["ALPHA", "BETA", "GAMMA", "DELTA", "EPSILON"], result.Products.Select(product => product.ProductCode));
        Assert.Equal(100m, result.Products[0].NetRevenue);
        Assert.Equal(80m, result.Products[2].GrossProfit);
    }

    [Fact]
    public async Task BudgetTargetLookupReturnsAnnualMonthlyAndControlledMissingResults()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        database.AddBudgetTarget(2026, null, 1_200m, 400m, "annual.md#target");
        database.AddBudgetTarget(2026, 7, 100m, 30m, "monthly.md#target");
        await database.SaveChangesAsync();
        var service = new SalesAnalysisService(database.Context, Clock);

        var annual = await service.GetBudgetTargetAsync(2026, null, CancellationToken.None);
        var monthly = await service.GetBudgetTargetAsync(2026, 7, CancellationToken.None);
        var missing = await service.GetBudgetTargetAsync(2027, null, CancellationToken.None);

        Assert.True(annual.IsAvailable);
        Assert.Equal(1_200m, annual.SalesTarget);
        Assert.Equal("annual.md#target", annual.AssumptionReference);
        Assert.True(monthly.IsAvailable);
        Assert.Equal(30m, monthly.ProfitTarget);
        Assert.False(missing.IsAvailable);
        Assert.Contains(missing.Warnings, warning => warning.Contains("No budget target", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ForecastUsesHistoricalTrendForFiveOrderedScenarioRows()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var product = await database.AddProductAsync("FORECAST", "Forecast");
        database.AddSale(product, "2023", new DateOnly(2023, 6, 1), 1, 100m, 0m, 10m);
        database.AddSale(product, "2024", new DateOnly(2024, 6, 1), 1, 200m, 0m, 10m);
        database.AddSale(product, "2025", new DateOnly(2025, 6, 1), 1, 300m, 0m, 10m);
        await database.SaveChangesAsync();
        var analysis = new SalesAnalysisService(database.Context, Clock);
        var service = new SalesForecastingService(analysis, Clock);

        var result = await service.ForecastAsync(CancellationToken.None);

        Assert.Equal("Ordinary least-squares linear regression", result.MethodName);
        Assert.Equal(2023, result.HistoricalPeriodStartYear);
        Assert.Equal(2025, result.HistoricalPeriodEndYear);
        Assert.Equal([2026, 2027, 2028, 2029, 2030], result.Forecasts.Select(forecast => forecast.Year));
        Assert.Equal(360m, result.Forecasts[0].ConservativeNetRevenue);
        Assert.Equal(400m, result.Forecasts[0].ExpectedNetRevenue);
        Assert.Equal(440m, result.Forecasts[0].OptimisticNetRevenue);
        Assert.All(result.Forecasts, forecast =>
        {
            Assert.True(forecast.ConservativeNetRevenue <= forecast.ExpectedNetRevenue);
            Assert.True(forecast.ExpectedNetRevenue <= forecast.OptimisticNetRevenue);
        });
        Assert.NotEmpty(result.Assumptions);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task ForecastReportsInsufficientDataWhenOnlyTwoHistoricalYearsExist()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var product = await database.AddProductAsync("FORECAST", "Forecast");
        database.AddSale(product, "2024", new DateOnly(2024, 6, 1), 1, 100m, 0m, 10m);
        database.AddSale(product, "2025", new DateOnly(2025, 6, 1), 1, 200m, 0m, 10m);
        await database.SaveChangesAsync();
        var analysis = new SalesAnalysisService(database.Context, Clock);

        var result = await new SalesForecastingService(analysis, Clock).ForecastAsync(CancellationToken.None);

        Assert.Empty(result.Forecasts);
        Assert.Contains(result.Warnings, warning => warning.Contains("At least three", StringComparison.Ordinal));
    }
}
