namespace CfoAgent.FinanceMcpServer.Configuration;

public sealed class FinanceOptions
{
    public const string SectionName = "Finance";

    public DateOnly DemoDate { get; init; }
}
