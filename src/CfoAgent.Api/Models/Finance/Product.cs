namespace CfoAgent.Api.Models.Finance;

public sealed class Product
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Sale> Sales { get; } = [];
}
