using CfoAgent.FinanceMcpServer.Data;
using CfoAgent.FinanceMcpServer.Models.Finance;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CfoAgent.Api.Tests.Finance;

internal sealed class TemporaryFinanceMcpDatabase : IAsyncDisposable
{
    private TemporaryFinanceMcpDatabase(string path, FinanceDbContext context)
    {
        Path = path;
        Context = context;
    }

    public string Path { get; }

    public FinanceDbContext Context { get; }

    public static async Task<TemporaryFinanceMcpDatabase> CreateAsync()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cfo-agent-finance-mcp-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        var context = new FinanceDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new TemporaryFinanceMcpDatabase(path, context);
    }

    public async Task<Product> AddProductAsync(string code, string name)
    {
        var product = new Product
        {
            Code = code,
            Name = name,
            Category = "Test",
            IsActive = true
        };
        Context.Products.Add(product);
        await Context.SaveChangesAsync();
        return product;
    }

    public void AddSale(
        Product product,
        string orderNumber,
        DateOnly saleDate,
        int quantity,
        decimal unitPrice,
        decimal discountAmount,
        decimal unitCost)
    {
        Context.Sales.Add(new Sale
        {
            Product = product,
            OrderNumber = orderNumber,
            SaleDate = saleDate,
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountAmount = discountAmount,
            UnitCost = unitCost,
            Region = "Test"
        });
    }

    public void AddBudgetTarget(int year, int? month, decimal salesTarget, decimal? profitTarget, string reference)
    {
        Context.BudgetTargets.Add(new BudgetTarget
        {
            Year = year,
            Month = month,
            SalesTarget = salesTarget,
            ProfitTarget = profitTarget,
            AssumptionReference = reference
        });
    }

    public Task SaveChangesAsync() => Context.SaveChangesAsync();

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(Path);
    }
}
