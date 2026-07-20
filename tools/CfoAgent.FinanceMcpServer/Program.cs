using CfoAgent.FinanceMcpServer.Configuration;
using CfoAgent.FinanceMcpServer.Data;
using CfoAgent.FinanceMcpServer.Health;
using CfoAgent.FinanceMcpServer.Data.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CfoAgent.FinanceMcpServer;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        if (args.SequenceEqual(["--list-tools"], StringComparer.Ordinal))
        {
            Console.Out.WriteLine(System.Text.Json.JsonSerializer.Serialize(FinanceMcpToolCatalog.Names));
            return;
        }

        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        var connectionString = FinanceDatabaseConfiguration.GetRequiredConnectionString(builder.Configuration);
        builder.Services.Configure<FinanceOptions>(builder.Configuration.GetSection(FinanceOptions.SectionName));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddDbContext<FinanceDbContext>(options => options.UseNpgsql(connectionString));
        builder.Services.AddScoped<DevelopmentFinanceSeeder>();
        builder.Services.AddHealthChecks()
            .AddCheck<FinanceDatabaseReadinessHealthCheck>("postgresql", tags: ["ready"]);
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithToolsFromAssembly();

        await using var host = builder.Build();
        if (args.SequenceEqual(["--migrate"], StringComparer.Ordinal)
            || args.SequenceEqual(["--seed"], StringComparer.Ordinal))
        {
            await InitializeDatabaseAsync(host.Services, seed: args[0] == "--seed");
            return;
        }

        host.MapMcp("/mcp");
        host.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });
        host.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready", StringComparer.Ordinal)
        });

        await host.RunAsync();
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider services, bool seed)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await dbContext.Database.MigrateAsync();
        if (seed)
        {
            await scope.ServiceProvider.GetRequiredService<DevelopmentFinanceSeeder>().SeedAsync(CancellationToken.None);
        }
    }
}
