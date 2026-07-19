using Microsoft.Extensions.Configuration;

namespace CfoAgent.FinanceMcpServer.Configuration;

public static class FinanceDatabaseConfiguration
{
    public const string ConnectionStringName = "FinanceDatabase";

    public const string EnvironmentVariableName = "ConnectionStrings__FinanceDatabase";

    public static string GetRequiredConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} is required.");
        }

        return connectionString;
    }
}
