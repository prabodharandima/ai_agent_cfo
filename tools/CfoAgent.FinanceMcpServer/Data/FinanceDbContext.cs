using CfoAgent.FinanceMcpServer.Models.Finance;
using Microsoft.EntityFrameworkCore;

namespace CfoAgent.FinanceMcpServer.Data;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<BudgetTarget> BudgetTargets => Set<BudgetTarget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureProduct(modelBuilder.Entity<Product>());
        ConfigureSale(modelBuilder.Entity<Sale>());
        ConfigureBudgetTarget(modelBuilder.Entity<BudgetTarget>());
    }

    private static void ConfigureProduct(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Product> entity)
    {
        entity.Property(product => product.Code).HasMaxLength(32).IsRequired();
        entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
        entity.Property(product => product.Category).HasMaxLength(100).IsRequired();
        entity.HasIndex(product => product.Code).IsUnique();
    }

    private static void ConfigureSale(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Sale> entity)
    {
        entity.Property(sale => sale.OrderNumber).HasMaxLength(64).IsRequired();
        entity.Property(sale => sale.Region).HasMaxLength(100).IsRequired();
        entity.Property(sale => sale.UnitPrice).HasPrecision(18, 2);
        entity.Property(sale => sale.DiscountAmount).HasPrecision(18, 2);
        entity.Property(sale => sale.UnitCost).HasPrecision(18, 2);
        entity.HasIndex(sale => sale.SaleDate);
        entity.HasIndex(sale => sale.OrderNumber);
        entity.HasIndex(sale => sale.ProductId);
        entity.HasOne(sale => sale.Product)
            .WithMany(product => product.Sales)
            .HasForeignKey(sale => sale.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Sales_Quantity_Positive", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_Sales_UnitPrice_NonNegative", "\"UnitPrice\" >= 0");
            table.HasCheckConstraint("CK_Sales_Discount_NonNegative", "\"DiscountAmount\" >= 0");
            table.HasCheckConstraint("CK_Sales_UnitCost_NonNegative", "\"UnitCost\" >= 0");
            table.HasCheckConstraint("CK_Sales_Discount_DoesNotExceedGross", "\"DiscountAmount\" <= \"Quantity\" * \"UnitPrice\"");
        });
    }

    private static void ConfigureBudgetTarget(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<BudgetTarget> entity)
    {
        entity.Property(target => target.SalesTarget).HasPrecision(18, 2);
        entity.Property(target => target.ProfitTarget).HasPrecision(18, 2);
        entity.Property(target => target.AssumptionReference).HasMaxLength(500).IsRequired();
        entity.HasIndex(target => target.Year)
            .HasFilter("\"Month\" IS NULL")
            .IsUnique();
        entity.HasIndex(target => new { target.Year, target.Month })
            .HasFilter("\"Month\" IS NOT NULL")
            .IsUnique();
        entity.ToTable(table =>
        {
            table.HasCheckConstraint("CK_BudgetTargets_Year_Positive", "\"Year\" > 0");
            table.HasCheckConstraint("CK_BudgetTargets_Month_Valid", "\"Month\" IS NULL OR (\"Month\" >= 1 AND \"Month\" <= 12)");
            table.HasCheckConstraint("CK_BudgetTargets_SalesTarget_NonNegative", "\"SalesTarget\" >= 0");
            table.HasCheckConstraint("CK_BudgetTargets_ProfitTarget_NonNegative", "\"ProfitTarget\" IS NULL OR \"ProfitTarget\" >= 0");
        });
    }
}
