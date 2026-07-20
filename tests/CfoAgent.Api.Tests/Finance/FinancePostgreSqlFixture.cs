using CfoAgent.FinanceMcpServer.Configuration;
using CfoAgent.FinanceMcpServer.Data;
using CfoAgent.FinanceMcpServer.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace CfoAgent.Api.Tests.Finance;

[CollectionDefinition(Name)]
public sealed class FinancePostgreSqlCollection : ICollectionFixture<FinancePostgreSqlFixture>
{
    public const string Name = "Finance PostgreSQL";
}

public sealed class FinancePostgreSqlFixture : IAsyncLifetime
{
    private static readonly DateOnly DemoDate = new(2026, 7, 15);
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17.7-alpine")
        .WithDatabase("cfo_agent_tests")
        .WithUsername("cfo_agent_tests")
        .WithPassword($"p8-{Guid.NewGuid():N}")
        .Build();
    private string? previousConnectionString;

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        previousConnectionString = Environment.GetEnvironmentVariable(FinanceDatabaseConfiguration.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(FinanceDatabaseConfiguration.EnvironmentVariableName, ConnectionString);

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
        await new DevelopmentFinanceSeeder(
                context,
                Options.Create(new FinanceOptions { DemoDate = DemoDate }),
                new FixedTimeProvider(DemoDate))
            .SeedAsync(CancellationToken.None);
    }

    public FinanceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new FinanceDbContext(options);
    }

    public async Task<(int Products, int Sales, int BudgetTargets)> GetCountsAsync()
    {
        await using var context = CreateDbContext();
        return (await context.Products.CountAsync(),
                await context.Sales.CountAsync(),
                await context.BudgetTargets.CountAsync());
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable(FinanceDatabaseConfiguration.EnvironmentVariableName, previousConnectionString);
        await container.DisposeAsync();
    }
}
