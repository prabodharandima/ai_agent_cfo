using System.Reflection;
using System.Text.Json;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Tests.Finance;
using CfoAgent.FinanceMcpServer;
using CfoAgent.KnowledgeFileMcpServer;

namespace CfoAgent.Api.Tests.PhaseEight;

public sealed class ContractFreezeTests
{
    private static readonly string[] FinanceToolNames =
    [
        "get_sales_summary",
        "compare_sales_periods",
        "get_top_products",
        "get_historical_sales",
        "get_budget_target"
    ];

    [Fact]
    public void FinanceToolCatalogAndTypedClientSurfaceAreFrozen()
    {
        Assert.Equal(FinanceToolNames, FinanceMcpToolCatalog.Names);
        Assert.Equal(FinanceToolNames.OrderBy(name => name, StringComparer.Ordinal), GetToolMethods(typeof(FinanceMcpTools)).Select(tool => tool.Name));

        AssertMethod(typeof(IFinanceMcpClient), "GetCurrentWeekSummaryAsync", typeof(SalesSummary), [typeof(CancellationToken)]);
        AssertMethod(typeof(IFinanceMcpClient), "GetWeekOverWeekComparisonAsync", typeof(WeeklySalesComparison), [typeof(CancellationToken)]);
        AssertMethod(typeof(IFinanceMcpClient), "GetCurrentMonthTopProductsAsync", typeof(TopProductsResult), [typeof(CancellationToken)]);
        AssertMethod(typeof(IFinanceMcpClient), "GetHistoricalYearlyTotalsAsync", typeof(HistoricalYearlySalesResult), [typeof(CancellationToken)]);
        AssertMethod(typeof(IFinanceMcpClient), "GetBudgetTargetAsync", typeof(BudgetTargetResult), [typeof(int), typeof(int?), typeof(CancellationToken)]);

        Assert.Equal(["startDate", "endDate", "cancellationToken"], GetToolParameterNames(typeof(FinanceMcpTools), "get_sales_summary"));
        Assert.Equal(["currentStartDate", "currentEndDate", "previousStartDate", "previousEndDate", "cancellationToken"], GetToolParameterNames(typeof(FinanceMcpTools), "compare_sales_periods"));
        Assert.Equal(["startDate", "endDate", "limit", "cancellationToken"], GetToolParameterNames(typeof(FinanceMcpTools), "get_top_products"));
        Assert.Equal(["startYear", "endYear", "cancellationToken"], GetToolParameterNames(typeof(FinanceMcpTools), "get_historical_sales"));
        Assert.Equal(["year", "month", "cancellationToken"], GetToolParameterNames(typeof(FinanceMcpTools), "get_budget_target"));
    }

    [Fact]
    public async Task FinanceToolOutputsAndControlledErrorsAreFrozen()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var product = await database.AddProductAsync("P8-ALPHA", "Phase Eight Alpha");
        database.AddSale(product, "P8-2021", new DateOnly(2021, 7, 15), 1, 100m, 0m, 40m);
        database.AddSale(product, "P8-2022", new DateOnly(2022, 7, 15), 1, 200m, 0m, 80m);
        database.AddSale(product, "P8-2023", new DateOnly(2023, 7, 15), 1, 300m, 0m, 120m);
        database.AddSale(product, "P8-2024", new DateOnly(2024, 7, 15), 1, 400m, 0m, 160m);
        database.AddSale(product, "P8-2025", new DateOnly(2025, 7, 15), 1, 500m, 0m, 200m);
        database.AddSale(product, "P8-2026", new DateOnly(2026, 7, 15), 2, 150m, 10m, 60m);
        database.AddBudgetTarget(2026, null, 3_000_000m, 900_000m, "current-budget-and-target.md#annual-target");
        await database.SaveChangesAsync();

        var tools = new FinanceMcpTools(database.Context);
        var summary = await tools.GetSalesSummaryAsync("2026-07-13", "2026-07-15", CancellationToken.None);
        var comparison = await tools.CompareSalesPeriodsAsync("2026-07-13", "2026-07-15", "2026-07-06", "2026-07-12", CancellationToken.None);
        var topProducts = await tools.GetTopProductsAsync("2026-07-01", "2026-07-15", cancellationToken: CancellationToken.None);
        var history = await tools.GetHistoricalSalesAsync(2021, 2025, CancellationToken.None);
        var budget = await tools.GetBudgetTargetAsync(2026, null, CancellationToken.None);

        Assert.True(summary.IsSuccess);
        Assert.IsType<McpSalesSummary>(summary.Data);
        Assert.Equal(290m, summary.Data!.NetRevenue);
        Assert.Equal("2026-07-13", summary.Data.Period.StartDate);
        Assert.True(comparison.IsSuccess);
        Assert.IsType<McpPeriodComparison>(comparison.Data);
        Assert.True(topProducts.IsSuccess);
        Assert.IsType<McpTopProducts>(topProducts.Data);
        Assert.True(history.IsSuccess);
        Assert.Equal([2021, 2022, 2023, 2024, 2025], history.Data!.Totals.Select(total => total.Year));
        Assert.True(budget.IsSuccess);
        Assert.True(budget.Data!.IsAvailable);
        Assert.Equal(3_000_000m, budget.Data.SalesTarget);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(summary, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(["data", "error", "isSuccess"], document.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        Assert.Equal(["averageOrderValue", "costOfGoodsSold", "grossMarginPercent", "grossProfit", "netRevenue", "orderCount", "period", "quantitySold", "topProduct", "warnings"], document.RootElement.GetProperty("data").EnumerateObject().Select(property => property.Name).OrderBy(name => name));

        var invalidSummary = await tools.GetSalesSummaryAsync("not-a-date", "2026-07-15", CancellationToken.None);
        var invalidLimit = await tools.GetTopProductsAsync("2026-07-01", "2026-07-15", 0, CancellationToken.None);
        var invalidHistory = await tools.GetHistoricalSalesAsync(2020, 2025, CancellationToken.None);
        var invalidBudget = await tools.GetBudgetTargetAsync(1999, null, CancellationToken.None);

        Assert.False(invalidSummary.IsSuccess);
        Assert.Equal("Dates must use YYYY-MM-DD format.", invalidSummary.Error);
        Assert.False(invalidLimit.IsSuccess);
        Assert.Equal("limit must be between 1 and 20.", invalidLimit.Error);
        Assert.False(invalidHistory.IsSuccess);
        Assert.Equal("Provide an inclusive year range from 2000 through 2100 containing at most five years.", invalidHistory.Error);
        Assert.False(invalidBudget.IsSuccess);
        Assert.Equal("year must be from 2000 through 2100 and month, when provided, must be from 1 through 12.", invalidBudget.Error);
    }

    [Fact]
    public async Task KnowledgeToolContractsAndReadOnlySecurityAreFrozen()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cfo-agent-p8-contract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "approved.md"), "approved knowledge");
            var tools = new KnowledgeFileMcpTools(new KnowledgeRoot(root));

            Assert.Equal(["list_knowledge_files", "read_knowledge_file"], GetToolMethods(typeof(KnowledgeFileMcpTools)).Select(tool => tool.Name));
            Assert.Equal(["cancellationToken"], GetToolParameterNames(typeof(KnowledgeFileMcpTools), "list_knowledge_files"));
            Assert.Equal(["relativePath", "cancellationToken"], GetToolParameterNames(typeof(KnowledgeFileMcpTools), "read_knowledge_file"));

            var files = await tools.ListFilesAsync(CancellationToken.None);
            var content = await tools.ReadFileAsync("approved.md", CancellationToken.None);
            var missing = await tools.ReadFileAsync("missing.md", CancellationToken.None);

            Assert.True(files.IsSuccess);
            Assert.Equal(["approved.md"], files.Data);
            Assert.True(content.IsSuccess);
            Assert.Equal("approved knowledge", content.Data);
            Assert.False(missing.IsSuccess);
            Assert.Equal("The requested knowledge file does not exist.", missing.Error);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tools.ReadFileAsync("../outside.md", CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tools.ReadFileAsync(Path.GetFullPath(Path.Combine(root, "approved.md")), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static IReadOnlyList<(string Name, MethodInfo Method)> GetToolMethods(Type type) => type
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Select(method => (Method: method, Attribute: method.GetCustomAttributes().SingleOrDefault(attribute => attribute.GetType().Name == "McpServerToolAttribute")))
        .Where(item => item.Attribute is not null)
        .Select(item => ((string)item.Attribute!.GetType().GetProperty("Name")!.GetValue(item.Attribute)!, item.Method))
        .OrderBy(item => item.Item1, StringComparer.Ordinal)
        .Select(item => (item.Item1, item.Method))
        .ToArray();

    private static IReadOnlyList<string> GetToolParameterNames(Type type, string toolName) => GetToolMethods(type)
        .Single(tool => tool.Name == toolName)
        .Method
        .GetParameters()
        .Select(parameter => parameter.Name!)
        .ToArray();

    private static void AssertMethod(Type type, string name, Type resultType, IReadOnlyList<Type> parameterTypes)
    {
        var method = Assert.Single(type.GetMethods(), method => method.Name == name);
        Assert.Equal(typeof(Task<>).MakeGenericType(resultType), method.ReturnType);
        Assert.Equal(parameterTypes, method.GetParameters().Select(parameter => parameter.ParameterType));
    }
}
