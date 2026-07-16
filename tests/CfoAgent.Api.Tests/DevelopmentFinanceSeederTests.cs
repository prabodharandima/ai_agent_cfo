using CfoAgent.Api.Configuration;
using CfoAgent.Api.Data;
using CfoAgent.Api.Data.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests;

public class DevelopmentFinanceSeederTests
{
    [Fact]
    public async Task SeedsDeterministicDataWithoutDuplicates()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cfo-agent-seed-{Guid.NewGuid():N}.db");
        var demoDate = new DateOnly(2026, 7, 15);

        try
        {
            var options = new DbContextOptionsBuilder<FinanceDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using (var dbContext = new FinanceDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                var seeder = new DevelopmentFinanceSeeder(dbContext, Options.Create(new FinanceOptions { DemoDate = demoDate }));

                await seeder.SeedAsync(CancellationToken.None);

                var firstProductCount = await dbContext.Products.CountAsync();
                var firstSaleCount = await dbContext.Sales.CountAsync();
                var firstBudgetTargetCount = await dbContext.BudgetTargets.CountAsync();

                await seeder.SeedAsync(CancellationToken.None);

                Assert.Equal(8, firstProductCount);
                Assert.Equal(18, firstBudgetTargetCount);
                Assert.Equal(firstProductCount, await dbContext.Products.CountAsync());
                Assert.Equal(firstSaleCount, await dbContext.Sales.CountAsync());
                Assert.Equal(firstBudgetTargetCount, await dbContext.BudgetTargets.CountAsync());
                Assert.Equal(new DateOnly(2021, 1, 5), await dbContext.Sales.MinAsync(sale => sale.SaleDate));
                Assert.Equal(demoDate, await dbContext.Sales.MaxAsync(sale => sale.SaleDate));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }
}
