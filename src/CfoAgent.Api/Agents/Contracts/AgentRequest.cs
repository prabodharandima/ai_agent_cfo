namespace CfoAgent.Api.Agents.Contracts;

public sealed record AgentRequest(
    string Message,
    string? ConversationId = null,
    object? StructuredData = null);
