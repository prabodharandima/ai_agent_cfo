using CfoAgent.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CfoAgent.Api.Tests;

public class FinanceDbContextMigrationTests
{
    [Fact]
    public async Task AppliesTheInitialFinanceSchemaMigrationToAnEmptyDatabase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cfo-agent-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<FinanceDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using (var dbContext = new FinanceDbContext(options))
            {
                await dbContext.Database.MigrateAsync();

                var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();

                Assert.Contains(appliedMigrations, migration => migration.EndsWith("InitialFinanceSchema", StringComparison.Ordinal));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }
}
