using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Tests.Finance;
using System.Text.Json;

namespace CfoAgent.Api.Tests.PhaseEight;

public sealed class ApiFinancePersistenceBoundaryTests
{
    [Fact]
    public void ApiAssemblyAndProjectHaveNoFinancePersistenceProviderDependency()
    {
        var assemblyNames = typeof(Program).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToArray();

        Assert.DoesNotContain(assemblyNames, name => name?.Contains("EntityFrameworkCore", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(assemblyNames, name => name?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(assemblyNames, name => name?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true);

        var project = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "CfoAgent.Api", "CfoAgent.Api.csproj"));
        Assert.DoesNotContain("EntityFrameworkCore", project, StringComparison.Ordinal);
        Assert.DoesNotContain("SQLitePCL", project, StringComparison.Ordinal);
        Assert.DoesNotContain("Npgsql", project, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiSourceNoLongerContainsFinancePersistenceDirectories()
    {
        var apiRoot = Path.Combine(FindRepositoryRoot(), "src", "CfoAgent.Api");

        Assert.False(Directory.Exists(Path.Combine(apiRoot, "Data")));
        Assert.False(Directory.Exists(Path.Combine(apiRoot, "Models", "Finance")));
        Assert.False(File.Exists(Path.Combine(apiRoot, "Features", "Sales", "SalesAnalysisService.cs")));

        var program = File.ReadAllText(Path.Combine(apiRoot, "Program.cs"));
        Assert.DoesNotContain("AddDbContext", program, StringComparison.Ordinal);
        Assert.DoesNotContain("--seed", program, StringComparison.Ordinal);

        using var appSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(apiRoot, "appsettings.json")));
        Assert.False(appSettings.RootElement.TryGetProperty("Database", out _));
        Assert.DoesNotContain("ConnectionString", appSettings.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public void ForecastRemainsDeterministicForHistoricalTotalsReturnedByFinanceMcp()
    {
        var service = new SalesForecastingService();
        var historical = new HistoricalYearlySalesResult(
            [new(2021, 100m), new(2022, 200m), new(2023, 300m), new(2024, 400m), new(2025, 500m)],
            Array.Empty<string>());

        var first = service.Forecast(historical);
        var second = service.Forecast(historical);

        Assert.Equal([600m, 700m, 800m, 900m, 1_000m], first.Forecasts.Select(row => row.ExpectedNetRevenue));
        Assert.Equal(first.Forecasts, second.Forecasts);
        Assert.Contains(first.Assumptions, value => value.Contains("do not include an LLM", StringComparison.Ordinal));
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
}
