using CfoAgent.Api.Rag.Embeddings;
using Microsoft.Extensions.AI;

namespace CfoAgent.Api.Tests.Rag.Embeddings;

public sealed class DeterministicTokenHashEmbeddingGeneratorTests
{
    [Fact]
    public void Generate_ReturnsTheSameVectorForTheSameText()
    {
        var generator = new DeterministicTokenHashEmbeddingGenerator();

        var first = generator.Generate("Annual sales forecast for Northwind.");
        var second = generator.Generate("Annual sales forecast for Northwind.");

        Assert.Equal(first, second);
        Assert.Equal(DeterministicTokenHashEmbeddingGenerator.DefaultDimension, first.Length);
    }

    [Fact]
    public void Generate_NormalizesNonEmptyVectors()
    {
        var generator = new DeterministicTokenHashEmbeddingGenerator();

        var vector = generator.Generate("sales forecast budget");

        var magnitude = MathF.Sqrt(vector.Sum(value => value * value));
        Assert.Equal(1f, magnitude, 5);
    }

    [Fact]
    public void Generate_ProducesMoreSimilarVectorsForSharedTokens()
    {
        var generator = new DeterministicTokenHashEmbeddingGenerator();
        var target = generator.Generate("annual sales forecast target");
        var related = generator.Generate("sales target forecast plan");
        var unrelated = generator.Generate("commodity supply risk analysis");

        Assert.True(CosineSimilarity(target, related) > CosineSimilarity(target, unrelated));
    }

    [Fact]
    public async Task GenerateAsync_UsesTheMicrosoftEmbeddingContract()
    {
        IEmbeddingGenerator<string, Embedding<float>> generator = new DeterministicTokenHashEmbeddingGenerator();

        var embeddings = await generator.GenerateAsync(["sales forecast"]);

        Assert.Equal(DeterministicTokenHashEmbeddingGenerator.DefaultDimension, Assert.Single(embeddings).Vector.Length);
    }

    private static float CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right) =>
        left.Zip(right, (leftValue, rightValue) => leftValue * rightValue).Sum();
}
