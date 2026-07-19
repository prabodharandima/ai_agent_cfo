using CfoAgent.FinanceMcpServer.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CfoAgent.FinanceMcpServer.Data;

public sealed class FinanceDbContextFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    public FinanceDbContext CreateDbContext(string[] args)
    {
        var configured = Environment.GetEnvironmentVariable(FinanceDatabaseConfiguration.EnvironmentVariableName);
        var connectionString = string.IsNullOrWhiteSpace(configured)
            ? new NpgsqlConnectionStringBuilder { Host = "localhost", Database = "cfo_agent_design" }.ConnectionString
            : configured;
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new FinanceDbContext(options);
    }
}
