using CfoAgent.FinanceMcpServer.Configuration;
using CfoAgent.FinanceMcpServer.Models.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CfoAgent.FinanceMcpServer.Data.Seed;

public sealed class DevelopmentFinanceSeeder(FinanceDbContext dbContext, IOptions<FinanceOptions> financeOptions)
{
    private static readonly ProductDefinition[] ProductDefinitions =
    [
        new("FIN-001", "Ledger Pro", "Finance Platform", 320m, 118m),
        new("FIN-002", "Insight Analytics", "Analytics", 280m, 96m),
        new("FIN-003", "Growth Planner", "Planning", 240m, 84m),
        new("FIN-004", "Pulse Reporting", "Analytics", 180m, 62m),
        new("FIN-005", "Atlas Operations", "Operations", 210m, 73m),
        new("FIN-006", "Beacon Support", "Services", 150m, 51m),
        new("FIN-007", "Horizon Mobile", "Mobile", 130m, 45m),
        new("FIN-008", "Vertex Essentials", "Finance Platform", 110m, 38m)
    ];

    private static readonly string[] Regions = ["North", "South", "East", "West"];

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var demoDate = financeOptions.Value.DemoDate;
        var products = ProductDefinitions
            .Select(definition => new Product
            {
                Code = definition.Code,
                Name = definition.Name,
                Category = definition.Category,
                IsActive = true
            })
            .ToArray();

        dbContext.Products.AddRange(products);

        for (var year = demoDate.Year - 5; year < demoDate.Year; year++)
        {
            for (var month = 1; month <= 12; month++)
            {
                AddMonthlySales(products, year, month, [5, 20]);
            }
        }

        for (var month = 1; month <= demoDate.Month; month++)
        {
            var saleDays = new[] { 5, 20 }
                .Where(day => new DateOnly(demoDate.Year, month, day) <= demoDate)
                .ToArray();

            AddMonthlySales(products, demoDate.Year, month, saleDays);
        }

        AddWeeklyComparisonSales(products, demoDate);
        AddBudgetTargets(demoDate);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void AddMonthlySales(IReadOnlyList<Product> products, int year, int month, IReadOnlyList<int> saleDays)
    {
        foreach (var day in saleDays)
        {
            var saleDate = new DateOnly(year, month, day);
            var yearTrend = (year - 2021) * 3;
            var seasonalQuantity = GetSeasonalQuantityAdjustment(month);

            for (var productIndex = 0; productIndex < products.Count; productIndex++)
            {
                var definition = ProductDefinitions[productIndex];
                var quantity = 12 + yearTrend + seasonalQuantity + (productIndex * 2) + (day == 20 ? 3 : 0);
                var grossRevenue = quantity * definition.UnitPrice;
                var discount = (productIndex + month + day) % 4 == 0 ? grossRevenue * 0.04m : 0m;

                dbContext.Sales.Add(new Sale
                {
                    OrderNumber = $"ORD-{saleDate:yyyyMMdd}-{(productIndex / 2) + 1:D2}",
                    SaleDate = saleDate,
                    Product = products[productIndex],
                    Quantity = quantity,
                    UnitPrice = definition.UnitPrice,
                    DiscountAmount = discount,
                    UnitCost = definition.UnitCost,
                    Region = Regions[(year + month + productIndex + day) % Regions.Length]
                });
            }
        }
    }

    private void AddWeeklyComparisonSales(IReadOnlyList<Product> products, DateOnly demoDate)
    {
        var currentWeekStart = demoDate.AddDays(-((int)demoDate.DayOfWeek + 6) % 7);
        var previousWeekStart = currentWeekStart.AddDays(-7);

        AddWeekSales(products, previousWeekStart.AddDays(1), 9);
        AddWeekSales(products, previousWeekStart.AddDays(4), 11);
        AddWeekSales(products, currentWeekStart, 18);
        AddWeekSales(products, currentWeekStart.AddDays(1), 20);
        AddWeekSales(products, demoDate, 22);
    }

    private void AddWeekSales(IReadOnlyList<Product> products, DateOnly saleDate, int baseQuantity)
    {
        for (var productIndex = 0; productIndex < products.Count; productIndex++)
        {
            var definition = ProductDefinitions[productIndex];
            var quantity = baseQuantity + (productIndex * 2);
            var grossRevenue = quantity * definition.UnitPrice;
            var discount = productIndex % 3 == 0 ? grossRevenue * 0.03m : 0m;

            dbContext.Sales.Add(new Sale
            {
                OrderNumber = $"ORD-{saleDate:yyyyMMdd}-{(productIndex / 2) + 1:D2}",
                SaleDate = saleDate,
                Product = products[productIndex],
                Quantity = quantity,
                UnitPrice = definition.UnitPrice,
                DiscountAmount = discount,
                UnitCost = definition.UnitCost,
                Region = Regions[(saleDate.Day + productIndex) % Regions.Length]
            });
        }
    }

    private void AddBudgetTargets(DateOnly demoDate)
    {
        for (var year = demoDate.Year - 5; year <= demoDate.Year; year++)
        {
            var annualSalesTarget = 1_800_000m + ((year - 2021) * 240_000m);
            dbContext.BudgetTargets.Add(new BudgetTarget
            {
                Year = year,
                SalesTarget = annualSalesTarget,
                ProfitTarget = annualSalesTarget * 0.32m,
                AssumptionReference = year == demoDate.Year
                    ? "current-budget-and-target.md#annual-target"
                    : "annual-sales-report.md#budget-context"
            });
        }

        var currentYearAnnualTarget = 1_800_000m + ((demoDate.Year - 2021) * 240_000m);
        for (var month = 1; month <= 12; month++)
        {
            var monthlySalesTarget = (currentYearAnnualTarget / 12m) * GetMonthlyBudgetWeight(month);
            dbContext.BudgetTargets.Add(new BudgetTarget
            {
                Year = demoDate.Year,
                Month = month,
                SalesTarget = monthlySalesTarget,
                ProfitTarget = monthlySalesTarget * 0.32m,
                AssumptionReference = "current-budget-and-target.md#monthly-targets"
            });
        }
    }

    private static int GetSeasonalQuantityAdjustment(int month) => month switch
    {
        1 => -3,
        2 => -2,
        3 => 0,
        4 => 1,
        5 => 2,
        6 => 3,
        7 => 2,
        8 => 3,
        9 => 5,
        10 => 7,
        11 => 9,
        12 => 14,
        _ => throw new ArgumentOutOfRangeException(nameof(month))
    };

    private static decimal GetMonthlyBudgetWeight(int month) => month switch
    {
        1 => 0.85m,
        2 => 0.88m,
        3 => 0.94m,
        4 => 0.96m,
        5 => 0.98m,
        6 => 1.00m,
        7 => 1.02m,
        8 => 1.04m,
        9 => 1.06m,
        10 => 1.08m,
        11 => 1.09m,
        12 => 1.10m,
        _ => throw new ArgumentOutOfRangeException(nameof(month))
    };

    private sealed record ProductDefinition(string Code, string Name, string Category, decimal UnitPrice, decimal UnitCost);
}
