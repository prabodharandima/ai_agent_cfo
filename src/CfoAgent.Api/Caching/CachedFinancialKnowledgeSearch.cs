using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Caching;

public sealed class CachedFinancialKnowledgeSearch(
    IFinancialKnowledgeSearch innerSearch,
    IApplicationCache cache,
    IOptions<CacheOptions> cacheOptions,
    IOptions<RagOptions> ragOptions) : IFinancialKnowledgeSearch
{
    private const string KeyPrefix = "rag:v1:retrieval";
    private readonly CacheOptions cacheOptions = cacheOptions.Value;
    private readonly RagOptions ragOptions = ragOptions.Value;

    public Task<FinancialKnowledgeRetrievalResult> RetrieveAsync(
        FinancialKnowledgeQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);

        return cache.GetOrCreateAsync(
            CreateKey(query),
            TimeSpan.FromSeconds(cacheOptions.Rag.RetrievalTtlSeconds),
            token => innerSearch.RetrieveAsync(query, token),
            cancellationToken);
    }

    private string CreateKey(FinancialKnowledgeQuery query) => string.Join(
        ':',
        KeyPrefix,
        NormalizeIndexVersion(ragOptions.IndexVersion),
        Hash(Normalize(query.Query)),
        query.TopK.ToString(CultureInfo.InvariantCulture),
        HashOrNone(query.DocumentType),
        HashOrNone(query.Period),
        ragOptions.MaximumRetrievalDistance.ToString("R", CultureInfo.InvariantCulture));

    private static void ValidateQuery(FinancialKnowledgeQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Query);
        if (query.TopK is <= 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "TopK must be between 1 and 10.");
        }
    }

    private static string NormalizeIndexVersion(string value) => Uri.EscapeDataString(value.Trim().ToLowerInvariant());

    private static string HashOrNone(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : Hash(Normalize(value));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Normalize(string value) => string.Join(
        ' ',
        value.Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .ToUpperInvariant();
}
