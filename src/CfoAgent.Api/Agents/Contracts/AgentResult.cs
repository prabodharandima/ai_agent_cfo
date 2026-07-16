namespace CfoAgent.Api.Agents.Contracts;

public enum AgentResponseType
{
    SalesSummary,
    SalesComparison,
    TopProducts,
    Forecast,
    Knowledge,
    Mixed,
    Unsupported
}

public sealed record AgentDataPeriod(DateOnly? From, DateOnly? To, string? Label);

public sealed record AgentSource(string DocumentId, string DocumentName, string Section, string SourcePath, string? Period = null);

public sealed record AgentResult(
    string Answer,
    AgentResponseType ResponseType,
    IReadOnlyList<string> AgentNames,
    object? StructuredData,
    IReadOnlyList<AgentSource> Sources,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> Warnings,
    AgentDataPeriod? DataPeriod);
