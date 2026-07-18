namespace CfoAgent.Api.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "AI";

    public const string OllamaHttpClientName = "Ollama";

    public string Provider { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; }

    public double Temperature { get; init; }

    public int ContextLength { get; init; }

    public int SimulatedDelayMilliseconds { get; init; }

    public bool SimulateFailure { get; init; }
}
