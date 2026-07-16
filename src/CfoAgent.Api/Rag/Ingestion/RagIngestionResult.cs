namespace CfoAgent.Api.Rag.Ingestion;

public sealed record RagIngestionResult(
    int Documents,
    int ChunksAddedOrUpdated,
    int Skipped,
    int Failed,
    IReadOnlyList<RagIngestionFailure> Failures);

public sealed record RagIngestionFailure(string SourcePath, string Message);
