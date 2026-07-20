namespace CfoAgent.Api.AI;

public enum AiProviderFailureKind
{
    Unavailable,
    Timeout,
    InvalidResponse
}

public sealed class AiProviderException(string providerName, AiProviderFailureKind failureKind) : LlmDependencyException(GetSafeMessage(failureKind))
{
    public string ProviderName { get; } = providerName;

    public AiProviderFailureKind FailureKind { get; } = failureKind;

    private static string GetSafeMessage(AiProviderFailureKind failureKind) => failureKind switch
    {
        AiProviderFailureKind.Unavailable => "The model provider is unavailable.",
        AiProviderFailureKind.Timeout => "The model provider request timed out.",
        AiProviderFailureKind.InvalidResponse => "The model provider returned an invalid response.",
        _ => "The model provider request failed."
    };
}
