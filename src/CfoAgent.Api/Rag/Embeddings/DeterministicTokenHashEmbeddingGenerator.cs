using System.Text;
using Microsoft.Extensions.AI;

namespace CfoAgent.Api.Rag.Embeddings;

/// <summary>
/// Produces deterministic, normalized vectors for local development plumbing.
/// This is a token-hashing baseline, not a production semantic embedding model.
/// </summary>
public sealed class DeterministicTokenHashEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public const int DefaultDimension = 256;

    public DeterministicTokenHashEmbeddingGenerator(int dimension = DefaultDimension)
    {
        if (dimension <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dimension), "Embedding dimension must be greater than zero.");
        }

        Dimension = dimension;
    }

    public int Dimension { get; }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var embeddings = new GeneratedEmbeddings<Embedding<float>>();

        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            embeddings.Add(new Embedding<float>(Generate(value)));
        }

        return Task.FromResult(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    public float[] Generate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var vector = new float[Dimension];
        foreach (var token in Tokenize(text))
        {
            var hash = ComputeHash(token);
            var index = (int)(hash % (uint)Dimension);
            vector[index] += (hash & 1) == 0 ? 1f : -1f;
        }

        Normalize(vector);
        return vector;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var token = new StringBuilder();

        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                token.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (token.Length > 0)
            {
                yield return token.ToString();
                token.Clear();
            }
        }

        if (token.Length > 0)
        {
            yield return token.ToString();
        }
    }

    private static uint ComputeHash(string token)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;

        foreach (var value in Encoding.UTF8.GetBytes(token))
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }

    private static void Normalize(float[] vector)
    {
        var magnitudeSquared = vector.Sum(value => value * value);
        if (magnitudeSquared == 0)
        {
            return;
        }

        var magnitude = MathF.Sqrt(magnitudeSquared);
        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] /= magnitude;
        }
    }
}
