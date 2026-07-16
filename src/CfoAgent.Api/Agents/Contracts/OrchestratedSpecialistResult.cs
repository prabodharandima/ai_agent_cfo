namespace CfoAgent.Api.Agents.Contracts;

public sealed record OrchestratedSpecialistResult(
    string AgentName,
    AgentResponseType ResponseType,
    object? StructuredData);
