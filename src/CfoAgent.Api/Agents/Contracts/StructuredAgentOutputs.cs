namespace CfoAgent.Api.Agents.Contracts;

public sealed record IntentClassificationOutput(string? Intent);

public sealed record SalesSummaryDateRangeOutput(string? StartDate, string? EndDate);
