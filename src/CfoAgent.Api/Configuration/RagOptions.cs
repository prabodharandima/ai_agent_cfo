namespace CfoAgent.Api.Configuration;

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public string KnowledgeFilesRoot { get; init; } = string.Empty;

    public int MaxChunkCharacters { get; init; } = 1200;

    public int MaxKnowledgeContextCharacters { get; init; } = 4000;

    public float MaximumRetrievalDistance { get; init; } = 1.25f;
}
