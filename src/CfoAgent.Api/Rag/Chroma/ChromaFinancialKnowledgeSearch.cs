using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Rag.Chroma;

public sealed class ChromaFinancialKnowledgeSearch(
    ChromaClient chromaClient,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IOptions<RagOptions> options) : IFinancialKnowledgeSearch
{
    private readonly RagOptions _options = options.Value;

    public async Task<FinancialKnowledgeRetrievalResult> RetrieveAsync(
        FinancialKnowledgeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Query);
        if (query.TopK is <= 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "TopK must be between 1 and 10.");
        }

        var collection = await chromaClient.GetCollectionAsync(cancellationToken: cancellationToken);
        if (collection is null)
        {
            return Insufficient("No financial knowledge has been ingested.");
        }

        var embeddings = await embeddingGenerator.GenerateAsync([query.Query], cancellationToken: cancellationToken);
        var matches = await chromaClient.QueryAsync(collection, embeddings.Single().Vector.ToArray(), query.TopK, cancellationToken);
        var sources = matches
            .Select(MapSource)
            .Where(source => source is not null)
            .Cast<FinancialKnowledgeSource>()
            .Where(source => source.Distance <= _options.MaximumRetrievalDistance)
            .Where(source => MatchesFilters(source, query))
            .OrderBy(source => source.Distance)
            .ThenBy(source => source.SourcePath, StringComparer.Ordinal)
            .ThenBy(source => source.Section, StringComparer.Ordinal)
            .ThenBy(source => source.ChunkId, StringComparer.Ordinal)
            .GroupBy(source => (source.DocumentId, source.Section, source.SourcePath))
            .Select(group => group.First())
            .ToArray();

        return sources.Length == 0
            ? Insufficient("No sufficiently relevant financial knowledge was found.")
            : new FinancialKnowledgeRetrievalResult(sources, Array.Empty<string>());
    }

    private static FinancialKnowledgeSource? MapSource(ChromaQueryMatch match)
    {
        if (match.Document is null || match.Distance is not float distance || match.Metadata is null)
        {
            return null;
        }

        return TryGet(match.Metadata, "document_id", out var documentId)
            && TryGet(match.Metadata, "document_name", out var documentName)
            && TryGet(match.Metadata, "document_type", out var documentType)
            && TryGet(match.Metadata, "period", out var period)
            && TryGet(match.Metadata, "section", out var section)
            && TryGet(match.Metadata, "source_path", out var sourcePath)
                ? new FinancialKnowledgeSource(match.Id, documentId, documentName, documentType, period, section, sourcePath, match.Document, distance)
                : null;
    }

    private static bool MatchesFilters(FinancialKnowledgeSource source, FinancialKnowledgeQuery query) =>
        (string.IsNullOrWhiteSpace(query.DocumentType) || string.Equals(source.DocumentType, query.DocumentType, StringComparison.OrdinalIgnoreCase))
        && (string.IsNullOrWhiteSpace(query.Period) || string.Equals(source.Period, query.Period, StringComparison.OrdinalIgnoreCase));

    private static bool TryGet(IReadOnlyDictionary<string, string> metadata, string key, out string value) =>
        metadata.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);

    private static FinancialKnowledgeRetrievalResult Insufficient(string warning) =>
        new(Array.Empty<FinancialKnowledgeSource>(), [warning]);
}
