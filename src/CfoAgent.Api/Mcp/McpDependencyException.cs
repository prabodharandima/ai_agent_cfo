namespace CfoAgent.Api.Mcp;

public enum McpDependencyFailureKind
{
    Disabled,
    Unavailable,
    Timeout,
    CapabilityMismatch,
    InvalidResponse
}

public sealed class McpDependencyException : Exception
{
    public McpDependencyException(
        string dependencyName,
        McpDependencyFailureKind failureKind,
        Exception? innerException = null)
        : base($"The {dependencyName} dependency could not complete the request.", innerException)
    {
        DependencyName = dependencyName;
        FailureKind = failureKind;
    }

    public string DependencyName { get; }

    public McpDependencyFailureKind FailureKind { get; }
}
