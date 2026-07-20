namespace CfoAgent.Api.AI.Ollama;

public enum OllamaFailureKind
{
    Unavailable,
    Timeout,
    InvalidResponse
}

public sealed class OllamaProviderException : LlmDependencyException
{
    public OllamaProviderException(OllamaFailureKind failureKind)
        : base(GetSafeMessage(failureKind))
    {
        FailureKind = failureKind;
    }

    public OllamaFailureKind FailureKind { get; }

    private static string GetSafeMessage(OllamaFailureKind failureKind) => failureKind switch
    {
        OllamaFailureKind.Unavailable => "The Ollama provider is unavailable.",
        OllamaFailureKind.Timeout => "The Ollama provider request timed out.",
        OllamaFailureKind.InvalidResponse => "The Ollama provider returned an invalid response.",
        _ => "The Ollama provider request failed."
    };
}
