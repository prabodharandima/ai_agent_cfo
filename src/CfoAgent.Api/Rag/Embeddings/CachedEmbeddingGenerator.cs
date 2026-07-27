using System.Security.Cryptography;
using System.Text;
using CfoAgent.Api.Caching;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Rag.Embeddings;

/// <summary>
/// Caches successful individual embedding vectors without exposing a cache implementation to RAG callers.
/// </summary>
public sealed class CachedEmbeddingGenerator(
    IEmbeddingGenerator<string, Embedding<float>> inner,
    IApplicationCache cache,
    IOptions<CacheOptions> cacheOptions,
    string providerIdentity,
    int dimension) : IEmbeddingGenerator<string, Embedding<float>>
{
    private const string KeyPrefix = "embedding:v1";
    private readonly CacheOptions cacheOptions = cacheOptions.Value;

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerIdentity);
        if (dimension <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimension), "Embedding dimension must be greater than zero.");
        }

        var results = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            cancellationToken.ThrowIfCancellationRequested();

            var vector = await cache.GetOrCreateAsync(
                CreateKey(value),
                TimeSpan.FromSeconds(cacheOptions.Embeddings.TtlSeconds),
                token => GenerateSingleAsync(value, options, token),
                cancellationToken);

            // Never return the cached array itself: callers cannot mutate a later cached response.
            results.Add(new Embedding<float>(vector.ToArray()));
        }

        return results;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        inner.GetService(serviceType, serviceKey);

    public void Dispose()
    {
    }

    private async Task<float[]> GenerateSingleAsync(
        string value,
        EmbeddingGenerationOptions? options,
        CancellationToken cancellationToken)
    {
        var generated = await inner.GenerateAsync([value], options, cancellationToken);
        var embedding = generated.SingleOrDefault()
            ?? throw new InvalidOperationException("The embedding generator returned no vector for a single input value.");
        var vector = embedding.Vector.ToArray();
        if (vector.Length != dimension)
        {
            throw new InvalidOperationException(
                $"The embedding generator returned dimension {vector.Length}, but {dimension} was configured.");
        }

        return vector;
    }

    private string CreateKey(string value) => string.Join(
        ':',
        KeyPrefix,
        Sha256(providerIdentity),
        dimension.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Sha256(cacheOptions.Embeddings.Version),
        Sha256(NormalizeText(value)));

    private static string NormalizeText(string value) =>
        string.Join(' ', value
            .Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
