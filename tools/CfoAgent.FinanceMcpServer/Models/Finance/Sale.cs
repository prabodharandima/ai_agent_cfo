namespace CfoAgent.FinanceMcpServer.Models.Finance;

public sealed class Sale
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = string.Empty;

    public DateOnly SaleDate { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal UnitCost { get; set; }

    public string Region { get; set; } = string.Empty;
}
