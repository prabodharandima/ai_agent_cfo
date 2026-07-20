namespace CfoAgent.Api.AI.Ollama;

public sealed class OllamaOptions
{
    public const string HttpClientName = "Ollama";

    public string Model { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; }

    public double Temperature { get; init; }

    public int ContextLength { get; init; }

    public int MaxOutputTokens { get; init; }
}
