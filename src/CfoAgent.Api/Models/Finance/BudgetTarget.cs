namespace CfoAgent.Api.Models.Finance;

public sealed class BudgetTarget
{
    public int Id { get; set; }

    public int Year { get; set; }

    public int? Month { get; set; }

    public decimal SalesTarget { get; set; }

    public decimal? ProfitTarget { get; set; }

    public string AssumptionReference { get; set; } = string.Empty;
}
