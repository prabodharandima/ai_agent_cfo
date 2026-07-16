namespace CfoAgent.Api.Rag.Chroma;

public sealed record ChromaRecord(
    string Id,
    string Document,
    IReadOnlyList<float> Embedding,
    IReadOnlyDictionary<string, string>? Metadata = null);
