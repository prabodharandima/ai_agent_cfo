using CfoAgent.Api.Configuration;

namespace CfoAgent.Api.Tests.Rag.Ingestion;

public sealed class RagOptionsTests
{
    [Fact]
    public void GetChunkOverlapSize_RoundsFifteenPercentAwayFromZero()
    {
        var options = new RagOptions { MaxChunkCharacters = 10, ChunkOverlapPercentage = 15 };

        Assert.Equal(2, options.GetChunkOverlapSize());
    }

    [Fact]
    public void GetChunkOverlapSize_RejectsNegativeOverlap()
    {
        var options = new RagOptions { MaxChunkCharacters = 1000, ChunkOverlapPercentage = -1 };

        Assert.Throws<InvalidOperationException>(() => _ = options.GetChunkOverlapSize());
    }

    [Fact]
    public void GetChunkOverlapSize_RejectsAChunkSizeOfZero()
    {
        var options = new RagOptions { MaxChunkCharacters = 0, ChunkOverlapPercentage = 15 };

        Assert.Throws<InvalidOperationException>(() => _ = options.GetChunkOverlapSize());
    }

    [Theory]
    [InlineData(100)]
    [InlineData(150)]
    public void GetChunkOverlapSize_RejectsAnOverlapOfOneHundredPercentOrMore(int percentage)
    {
        var options = new RagOptions { MaxChunkCharacters = 1000, ChunkOverlapPercentage = percentage };

        Assert.Throws<InvalidOperationException>(() => _ = options.GetChunkOverlapSize());
    }
}
