namespace CfoAgent.Api.Rag.Chroma;

public sealed record ChromaQueryMatch(
    string Id,
    string? Document,
    IReadOnlyDictionary<string, string>? Metadata,
    float? Distance);
