using CfoAgent.Api.Tests.Finance;
using CfoAgent.FinanceMcpServer;
using CfoAgent.FinanceMcpServer.Configuration;
using CfoAgent.FinanceMcpServer.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.PhaseEight;

[Collection(FinancePostgreSqlCollection.Name)]
public sealed class FinanceMcpOwnershipTests(FinancePostgreSqlFixture postgres)
{
    private static readonly DateOnly DemoDate = new(2026, 7, 15);

    [Fact]
    public void FinanceMcpAssemblyDoesNotReferenceTheApiProject()
    {
        var referencedAssemblies = typeof(FinanceMcpTools).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain("CfoAgent.Api", referencedAssemblies);
    }

    [Fact]
    public async Task FinanceMcpOwnsDeterministicIdempotentSeedAndAllFiveQueries()
    {
        await using var context = postgres.CreateDbContext();
        var seeder = new DevelopmentFinanceSeeder(
            context,
            Options.Create(new FinanceOptions { DemoDate = DemoDate }),
            new FixedTimeProvider(DemoDate));

        await seeder.SeedAsync(CancellationToken.None);
        var firstCounts = await postgres.GetCountsAsync();
        await seeder.SeedAsync(CancellationToken.None);
        var secondCounts = await postgres.GetCountsAsync();

        Assert.Equal(8, firstCounts.Products);
        Assert.True(firstCounts.Sales >= 1_104);
        Assert.Equal(18, firstCounts.BudgetTargets);
        Assert.Equal(firstCounts, secondCounts);

        var tools = new FinanceMcpTools(context);
        var summary = await tools.GetSalesSummaryAsync("2026-07-13", "2026-07-15", CancellationToken.None);
        var comparison = await tools.CompareSalesPeriodsAsync("2026-07-13", "2026-07-15", "2026-07-06", "2026-07-12", CancellationToken.None);
        var topProducts = await tools.GetTopProductsAsync("2026-07-01", "2026-07-15", 5, CancellationToken.None);
        var history = await tools.GetHistoricalSalesAsync(2021, 2025, CancellationToken.None);
        var budget = await tools.GetBudgetTargetAsync(2026, null, CancellationToken.None);
        var unseededPeriod = await tools.GetSalesSummaryAsync("2026-07-16", "2026-07-16", CancellationToken.None);

        Assert.True(summary.IsSuccess);
        Assert.True(summary.Data!.NetRevenue > 0m);
        Assert.True(comparison.IsSuccess);
        Assert.NotNull(comparison.Data!.NetRevenueChangePercentage);
        Assert.True(topProducts.IsSuccess);
        Assert.Equal(5, topProducts.Data!.Products.Count);
        Assert.True(history.IsSuccess);
        Assert.Equal([2021, 2022, 2023, 2024, 2025], history.Data!.Totals.Select(total => total.Year));
        Assert.True(budget.IsSuccess);
        Assert.True(budget.Data!.IsAvailable);
        Assert.Equal(3_000_000m, budget.Data.SalesTarget);
        Assert.True(unseededPeriod.IsSuccess);
        Assert.Equal(0m, unseededPeriod.Data!.NetRevenue);
        Assert.Contains("No sales data is available for this period.", unseededPeriod.Data.Warnings);
    }

    [Fact]
    public async Task SeedBackfillsTheCurrentWeekForTheInjectedClockWithoutDuplicatingExistingRows()
    {
        await using var context = postgres.CreateDbContext();
        var currentDate = new DateOnly(2026, 7, 20);
        var seeder = new DevelopmentFinanceSeeder(
            context,
            Options.Create(new FinanceOptions { DemoDate = DemoDate }),
            new FixedTimeProvider(currentDate));

        var before = await context.Sales.CountAsync();
        await seeder.SeedAsync(CancellationToken.None);
        var afterFirstSeed = await context.Sales.CountAsync();
        await seeder.SeedAsync(CancellationToken.None);
        var afterSecondSeed = await context.Sales.CountAsync();
        var futureRows = await context.Sales.CountAsync(sale => sale.SaleDate > currentDate);

        var tools = new FinanceMcpTools(context);
        var summary = await tools.GetSalesSummaryAsync("2026-07-20", "2026-07-20", CancellationToken.None);

        Assert.True(afterFirstSeed >= before);
        Assert.Equal(afterFirstSeed, afterSecondSeed);
        Assert.Equal(0, futureRows);
        Assert.True(summary.IsSuccess);
        Assert.True(summary.Data!.NetRevenue > 0m);
        Assert.Empty(summary.Data.Warnings);
    }
}
