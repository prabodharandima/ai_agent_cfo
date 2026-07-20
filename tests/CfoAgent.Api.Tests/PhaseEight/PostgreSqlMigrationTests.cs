using System.Diagnostics;
using CfoAgent.Api.Tests.Finance;
using CfoAgent.FinanceMcpServer.Configuration;
using Microsoft.EntityFrameworkCore;

namespace CfoAgent.Api.Tests.PhaseEight;

[Collection(FinancePostgreSqlCollection.Name)]
public sealed class PostgreSqlMigrationTests(FinancePostgreSqlFixture postgres)
{
    [Fact]
    public async Task MigrationCreatesReproduciblePostgreSqlSchema()
    {
        await using var context = postgres.CreateDbContext();

        var applied = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
        var pending = (await context.Database.GetPendingMigrationsAsync()).ToArray();
        var columns = await ReadColumnTypesAsync(context);
        var indexes = await ReadNamesAsync(context, "SELECT indexname FROM pg_indexes WHERE schemaname = 'public'");
        var constraints = await ReadNamesAsync(context, "SELECT conname FROM pg_constraint WHERE connamespace = 'public'::regnamespace");

        Assert.Single(applied);
        Assert.EndsWith("_InitialPostgreSqlFinanceSchema", applied[0], StringComparison.Ordinal);
        Assert.Empty(pending);
        Assert.Equal("date", columns[("Sales", "SaleDate")].DataType);
        Assert.Equal(("numeric", 18, 2), columns[("Sales", "UnitPrice")]);
        Assert.Equal(("numeric", 18, 2), columns[("BudgetTargets", "SalesTarget")]);
        Assert.Contains("IX_BudgetTargets_Year", indexes);
        Assert.Contains("IX_BudgetTargets_Year_Month", indexes);
        Assert.Contains("IX_Products_Code", indexes);
        Assert.Contains("CK_Sales_Discount_DoesNotExceedGross", constraints);
        Assert.Contains("CK_BudgetTargets_Month_Valid", constraints);
        Assert.Contains("FK_Sales_Products_ProductId", constraints);
    }

    [Fact]
    public async Task ExplicitMigrateAndSeedCommandsAreNonDestructiveAndIdempotent()
    {
        var before = await postgres.GetCountsAsync();

        await RunFinanceCommandAsync("--migrate");
        await RunFinanceCommandAsync("--seed");
        var afterFirstSeed = await postgres.GetCountsAsync();
        await RunFinanceCommandAsync("--seed");

        Assert.Equal(before.Products, afterFirstSeed.Products);
        Assert.True(afterFirstSeed.Sales >= before.Sales);
        Assert.Equal(before.BudgetTargets, afterFirstSeed.BudgetTargets);
        Assert.Equal(afterFirstSeed, await postgres.GetCountsAsync());
    }

    private async Task RunFinanceCommandAsync(string command)
    {
        var assemblyPath = Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "CfoAgent.FinanceMcpServer",
            "bin",
            GetBuildConfiguration(),
            "net10.0",
            "CfoAgent.FinanceMcpServer.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.ArgumentList.Add(command);
        startInfo.Environment[FinanceDatabaseConfiguration.EnvironmentVariableName] = postgres.ConnectionString;
        startInfo.Environment["Finance__DemoDate"] = "2026-07-15";

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The Finance MCP database command could not be started.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        var standardError = await process.StandardError.ReadToEndAsync(timeout.Token);

        Assert.True(process.ExitCode == 0, $"Finance MCP command {command} failed: {standardError}");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CfoAgent.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The repository root could not be located.");
    }

    private static string GetBuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static async Task<Dictionary<(string Table, string Column), (string DataType, int? Precision, int? Scale)>> ReadColumnTypesAsync(
        Microsoft.EntityFrameworkCore.DbContext context)
    {
        var result = new Dictionary<(string, string), (string, int?, int?)>();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT table_name, column_name, data_type, numeric_precision, numeric_scale
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name IN ('Sales', 'BudgetTargets')
            """;
        await context.Database.OpenConnectionAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[(reader.GetString(0), reader.GetString(1))] = (
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4));
        }

        return result;
    }

    private static async Task<string[]> ReadNamesAsync(Microsoft.EntityFrameworkCore.DbContext context, string sql)
    {
        var result = new List<string>();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        if (context.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
        {
            await context.Database.OpenConnectionAsync();
        }

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result.ToArray();
    }
}
