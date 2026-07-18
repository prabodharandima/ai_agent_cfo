using CfoAgent.FinanceMcpServer.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CfoAgent.FinanceMcpServer;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.SequenceEqual(["--list-tools"], StringComparer.Ordinal))
        {
            Console.Out.WriteLine(System.Text.Json.JsonSerializer.Serialize(FinanceMcpToolCatalog.Names));
            return;
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        var databasePath = FindDatabasePath();
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        builder.Services.AddDbContext<FinanceDbContext>(options => options.UseSqlite(connectionString));
        builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        await builder.Build().RunAsync();
    }

    private static string FindDatabasePath()
    {
        foreach (var startDirectory in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "data", "cfo-agent.db");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new FileNotFoundException("The development SQLite database was not found under a data directory.");
    }
}
