namespace CfoAgent.Api.Agents.Contracts;

public sealed record AgentSessionTurn(AgentResponseType ResponseType, AgentDataPeriod? DataPeriod);

public sealed record AgentSessionContext(IReadOnlyList<AgentSessionTurn> Turns)
{
    public static AgentSessionContext Empty { get; } = new(Array.Empty<AgentSessionTurn>());
}
