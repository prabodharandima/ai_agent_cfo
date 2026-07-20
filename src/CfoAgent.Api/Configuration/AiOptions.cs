using CfoAgent.Api.AI.Ollama;

namespace CfoAgent.Api.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "AI";

    public string Provider { get; init; } = string.Empty;

    public OllamaOptions Ollama { get; init; } = new();
}
