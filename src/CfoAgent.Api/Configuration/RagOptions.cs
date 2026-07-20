namespace CfoAgent.Api.Configuration;

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public string KnowledgeFilesRoot { get; init; } = string.Empty;

    public int MaxChunkCharacters { get; init; } = 1200;

    public int ChunkOverlapPercentage { get; init; } = 15;

    public int MaxKnowledgeContextCharacters { get; init; } = 4000;

    public float MaximumRetrievalDistance { get; init; } = 1.25f;

    public int GetChunkOverlapSize()
    {
        if (MaxChunkCharacters <= 0)
        {
            throw new InvalidOperationException("Rag:MaxChunkCharacters must be greater than zero.");
        }

        if (ChunkOverlapPercentage < 0 || ChunkOverlapPercentage >= 100)
        {
            throw new InvalidOperationException("Rag:ChunkOverlapPercentage must be at least zero and less than 100.");
        }

        var overlapSize = checked((int)Math.Round(
            MaxChunkCharacters * (ChunkOverlapPercentage / 100d),
            MidpointRounding.AwayFromZero));

        if (overlapSize >= MaxChunkCharacters)
        {
            throw new InvalidOperationException("Rag:ChunkOverlapPercentage must produce an overlap smaller than Rag:MaxChunkCharacters.");
        }

        return overlapSize;
    }
}
