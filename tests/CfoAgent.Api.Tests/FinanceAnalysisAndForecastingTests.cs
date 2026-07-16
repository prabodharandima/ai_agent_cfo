using CfoAgent.Api.Configuration;
using CfoAgent.Api.Data;
using CfoAgent.Api.Data.Seed;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests;

public class FinanceAnalysisAndForecastingTests
{
    private static readonly DateOnly DemoDate = new(2026, 7, 15);
    private static readonly TimeProvider Clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task AnalysisUsesInjectedWeekBoundariesAndReturnsSeededData()
    {
        await using var database = await SeededDatabase.CreateAsync(DemoDate);
        var service = new SalesAnalysisService(database.Context, Clock);

        var summary = await service.GetCurrentWeekSummaryAsync(CancellationToken.None);
        var comparison = await service.GetWeekOverWeekComparisonAsync(CancellationToken.None);
        var topProducts = await service.GetCurrentMonthTopProductsAsync(CancellationToken.None);
        var historical = await service.GetHistoricalYearlyTotalsAsync(CancellationToken.None);
        var annualBudget = await service.GetBudgetTargetAsync(2026, null, CancellationToken.None);
        var monthlyBudget = await service.GetBudgetTargetAsync(2026, 7, CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 13), summary.Period.StartDate);
        Assert.Equal(DemoDate, summary.Period.EndDate);
        Assert.True(summary.NetRevenue > 0m);
        Assert.Equal(new DateOnly(2026, 7, 6), comparison.PreviousWeek.Period.StartDate);
        Assert.Equal(new DateOnly(2026, 7, 12), comparison.PreviousWeek.Period.EndDate);
        Assert.NotNull(comparison.NetRevenueChangePercentage);
        Assert.Equal(5, topProducts.Products.Count);
        Assert.True(topProducts.Products.SequenceEqual(topProducts.Products.OrderByDescending(product => product.NetRevenue).ThenBy(product => product.ProductCode, StringComparer.Ordinal)));
        Assert.Equal([2021, 2022, 2023, 2024, 2025], historical.Totals.Select(total => total.Year));
        Assert.True(annualBudget.IsAvailable);
        Assert.Equal(3_000_000m, annualBudget.SalesTarget);
        Assert.True(monthlyBudget.IsAvailable);
        Assert.NotNull(monthlyBudget.ProfitTarget);
    }

    [Fact]
    public async Task ComparisonReturnsZeroPercentWhenBothWeeksHaveNoRevenue()
    {
        await using var database = await SeededDatabase.CreateEmptyAsync();
        var service = new SalesAnalysisService(database.Context, Clock);

        var comparison = await service.GetWeekOverWeekComparisonAsync(CancellationToken.None);

        Assert.Equal(0m, comparison.NetRevenueChangePercentage);
        Assert.Equal(SalesChangeDirection.Unchanged, comparison.Direction);
        Assert.Equal(0m, comparison.CurrentWeek.GrossMarginPercent);
    }

    [Fact]
    public async Task ForecastReturnsFiveDeterministicScenarioRows()
    {
        await using var database = await SeededDatabase.CreateAsync(DemoDate);
        var analysis = new SalesAnalysisService(database.Context, Clock);
        var service = new SalesForecastingService(analysis, Clock);

        var first = await service.ForecastAsync(CancellationToken.None);
        var second = await service.ForecastAsync(CancellationToken.None);

        Assert.Equal("Ordinary least-squares linear regression", first.MethodName);
        Assert.Equal(2021, first.HistoricalPeriodStartYear);
        Assert.Equal(2025, first.HistoricalPeriodEndYear);
        Assert.Equal(5, first.Forecasts.Count);
        Assert.Equal([2026, 2027, 2028, 2029, 2030], first.Forecasts.Select(forecast => forecast.Year));
        Assert.All(first.Forecasts, forecast =>
        {
            Assert.True(forecast.ConservativeNetRevenue <= forecast.ExpectedNetRevenue);
            Assert.True(forecast.ExpectedNetRevenue <= forecast.OptimisticNetRevenue);
        });
        Assert.Equal(first.HistoricalInputs, second.HistoricalInputs);
        Assert.Equal(first.Forecasts, second.Forecasts);
    }

    [Fact]
    public async Task ForecastReportsInsufficientHistoricalData()
    {
        await using var database = await SeededDatabase.CreateEmptyAsync();
        var analysis = new SalesAnalysisService(database.Context, Clock);
        var service = new SalesForecastingService(analysis, Clock);

        var result = await service.ForecastAsync(CancellationToken.None);

        Assert.Empty(result.Forecasts);
        Assert.Contains(result.Warnings, warning => warning.Contains("At least three", StringComparison.Ordinal));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SeededDatabase : IAsyncDisposable
    {
        private SeededDatabase(string path, FinanceDbContext context)
        {
            Path = path;
            Context = context;
        }

        public string Path { get; }

        public FinanceDbContext Context { get; }

        public static async Task<SeededDatabase> CreateAsync(DateOnly demoDate)
        {
            var database = await CreateEmptyAsync();
            var seeder = new DevelopmentFinanceSeeder(database.Context, Options.Create(new FinanceOptions { DemoDate = demoDate }));
            await seeder.SeedAsync(CancellationToken.None);
            return database;
        }

        public static async Task<SeededDatabase> CreateEmptyAsync()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cfo-agent-analysis-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<FinanceDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            var context = new FinanceDbContext(options);
            await context.Database.MigrateAsync();
            return new SeededDatabase(path, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            SqliteConnection.ClearAllPools();
            File.Delete(Path);
        }
    }
}
