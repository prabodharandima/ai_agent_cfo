namespace CfoAgent.Api.Configuration;

public sealed class AgentSessionOptions
{
    public const string SectionName = "AgentSessions";

    public int MessageLimit { get; init; } = 8;

    public int ExpirationMinutes { get; init; } = 30;

    public int MaximumSessions { get; init; } = 1_000;
}
