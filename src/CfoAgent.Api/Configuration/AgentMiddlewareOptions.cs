namespace CfoAgent.Api.Configuration;

public sealed class AgentMiddlewareOptions
{
    public const string SectionName = "AgentMiddleware";

    public bool PromptInjectionCheckEnabled { get; init; } = true;

    public IReadOnlyList<string> SuspiciousPromptPhrases { get; init; } = Array.Empty<string>();
}
