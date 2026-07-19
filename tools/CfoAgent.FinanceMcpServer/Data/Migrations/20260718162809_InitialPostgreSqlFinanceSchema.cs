using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CfoAgent.FinanceMcpServer.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSqlFinanceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: true),
                    SalesTarget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProfitTarget = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AssumptionReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetTargets", x => x.Id);
                    table.CheckConstraint("CK_BudgetTargets_Month_Valid", "\"Month\" IS NULL OR (\"Month\" >= 1 AND \"Month\" <= 12)");
                    table.CheckConstraint("CK_BudgetTargets_ProfitTarget_NonNegative", "\"ProfitTarget\" IS NULL OR \"ProfitTarget\" >= 0");
                    table.CheckConstraint("CK_BudgetTargets_SalesTarget_NonNegative", "\"SalesTarget\" >= 0");
                    table.CheckConstraint("CK_BudgetTargets_Year_Positive", "\"Year\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SaleDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.CheckConstraint("CK_Sales_Discount_DoesNotExceedGross", "\"DiscountAmount\" <= \"Quantity\" * \"UnitPrice\"");
                    table.CheckConstraint("CK_Sales_Discount_NonNegative", "\"DiscountAmount\" >= 0");
                    table.CheckConstraint("CK_Sales_Quantity_Positive", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_Sales_UnitCost_NonNegative", "\"UnitCost\" >= 0");
                    table.CheckConstraint("CK_Sales_UnitPrice_NonNegative", "\"UnitPrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_Sales_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTargets_Year",
                table: "BudgetTargets",
                column: "Year",
                unique: true,
                filter: "\"Month\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetTargets_Year_Month",
                table: "BudgetTargets",
                columns: new[] { "Year", "Month" },
                unique: true,
                filter: "\"Month\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                table: "Products",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_OrderNumber",
                table: "Sales",
                column: "OrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_ProductId",
                table: "Sales",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_SaleDate",
                table: "Sales",
                column: "SaleDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetTargets");

            migrationBuilder.DropTable(
                name: "Sales");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
