using System.Net;
using System.Text;
using System.Text.Json;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Chroma;
using CfoAgent.Api.Rag.Embeddings;
using CfoAgent.Api.Rag.Ingestion;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Rag.Ingestion;

public sealed class RagDocumentIngestionServiceTests
{
    [Fact]
    public async Task IngestAsync_UpsertsHeadingChunksWithStableIdsAndSourceMetadata()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "budget.md"), ValidDocument);
        var handler = new RecordingHandler();
        var service = CreateService(directory.Path, handler);

        var first = await service.IngestAsync();
        var second = await service.IngestAsync();

        Assert.Equal(1, first.Documents);
        Assert.Equal(2, first.ChunksAddedOrUpdated);
        Assert.Equal(0, first.Failed);
        Assert.Equal(first.ChunksAddedOrUpdated, second.ChunksAddedOrUpdated);
        Assert.Equal(2, handler.UpsertBodies.Count);

        using var firstBody = JsonDocument.Parse(handler.UpsertBodies[0]);
        using var secondBody = JsonDocument.Parse(handler.UpsertBodies[1]);
        var firstIds = firstBody.RootElement.GetProperty("ids").EnumerateArray().Select(value => value.GetString()).ToArray();
        var secondIds = secondBody.RootElement.GetProperty("ids").EnumerateArray().Select(value => value.GetString()).ToArray();
        var firstMetadata = firstBody.RootElement.GetProperty("metadatas")[0];

        Assert.Equal(firstIds, secondIds);
        Assert.All(firstIds, id => Assert.StartsWith("chunk-", id));
        Assert.Equal("Current Budget", firstMetadata.GetProperty("document_name").GetString());
        Assert.Equal("budget_target", firstMetadata.GetProperty("document_type").GetString());
        Assert.Equal("2026", firstMetadata.GetProperty("period").GetString());
        Assert.Equal("data/knowledge/budget.md", firstMetadata.GetProperty("source_path").GetString());
        Assert.Equal("Annual Target", firstMetadata.GetProperty("section").GetString());
    }

    [Fact]
    public async Task IngestAsync_ReportsTheSourceDocumentWhenFrontMatterIsInvalid()
    {
        using var directory = new TemporaryDirectory();
        var invalidPath = Path.Combine(directory.Path, "invalid.md");
        await File.WriteAllTextAsync(invalidPath, "# Missing front matter");
        var service = CreateService(directory.Path, new RecordingHandler());

        var result = await service.IngestAsync();

        var failure = Assert.Single(result.Failures);
        Assert.Equal(1, result.Documents);
        Assert.Equal(1, result.Failed);
        Assert.Equal(invalidPath, failure.SourcePath);
        Assert.Contains("front matter", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestAsync_UsesNonOverlappingSlidingWindowsWhenOverlapIsZero()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "alphabet.md"), CreateDocument("abcdefghijklmnopqrstuvwxyz"));
        var handler = new RecordingHandler();

        var result = await CreateService(directory.Path, handler, chunkSize: 10, overlapPercentage: 0).IngestAsync();

        Assert.Equal(3, result.ChunksAddedOrUpdated);
        Assert.Equal(["abcdefghij", "klmnopqrst", "uvwxyz"], ReadDocuments(handler.UpsertBodies.Single()));
        Assert.Equal([0, 10, 20], ReadOffsets(handler.UpsertBodies.Single(), "chunk_start"));
        Assert.Equal([10, 20, 26], ReadOffsets(handler.UpsertBodies.Single(), "chunk_end"));
    }

    [Fact]
    public async Task IngestAsync_UsesFifteenPercentOverlapAndCoversTheFinalTextRange()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "alphabet.md"), CreateDocument("abcdefghijklmnopqrstuvwxyz"));
        var handler = new RecordingHandler();

        var result = await CreateService(directory.Path, handler, chunkSize: 10, overlapPercentage: 15).IngestAsync();

        Assert.Equal(3, result.ChunksAddedOrUpdated);
        var documents = ReadDocuments(handler.UpsertBodies.Single());
        var starts = ReadOffsets(handler.UpsertBodies.Single(), "chunk_start");
        var ends = ReadOffsets(handler.UpsertBodies.Single(), "chunk_end");

        Assert.Equal(["abcdefghij", "ijklmnopqr", "qrstuvwxyz"], documents);
        Assert.Equal([0, 8, 16], starts);
        Assert.Equal([10, 18, 26], ends);
        Assert.Equal(documents[0][^2..], documents[1][..2]);
        Assert.Equal(documents[1][^2..], documents[2][..2]);
        Assert.Equal(0, starts[0]);
        Assert.Equal(26, ends[^1]);
        Assert.All(Enumerable.Range(1, starts.Length - 1), index => Assert.True(starts[index] <= ends[index - 1]));
    }

    [Theory]
    [InlineData("short", 1)]
    [InlineData("exactly-ten", 1)]
    public async Task IngestAsync_CreatesOneChunkForShortOrExactSizeDocuments(string content, int expectedChunks)
    {
        using var directory = new TemporaryDirectory();
        var text = content == "exactly-ten" ? "abcdefghij" : content;
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "document.md"), CreateDocument(text));
        var handler = new RecordingHandler();

        var result = await CreateService(directory.Path, handler, chunkSize: 10, overlapPercentage: 15).IngestAsync();

        Assert.Equal(expectedChunks, result.ChunksAddedOrUpdated);
        Assert.Equal([text], ReadDocuments(handler.UpsertBodies.Single()));
    }

    [Fact]
    public async Task IngestAsync_ReplacesPreviousSourceChunksWhenTheChunkConfigurationChanges()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "alphabet.md"), CreateDocument("abcdefghijklmnopqrstuvwxyz"));
        var handler = new RecordingHandler();

        await CreateService(directory.Path, handler, chunkSize: 10, overlapPercentage: 0).IngestAsync();
        await CreateService(directory.Path, handler, chunkSize: 10, overlapPercentage: 15).IngestAsync();

        Assert.Equal(["delete", "upsert", "delete", "upsert"], handler.Operations);
        Assert.Equal(2, handler.DeleteBodies.Count);
        Assert.All(handler.DeleteBodies, body => Assert.Contains("\"source_path\":\"data/knowledge/test.md\"", body, StringComparison.Ordinal));
        Assert.NotEqual(ReadIds(handler.UpsertBodies[0]), ReadIds(handler.UpsertBodies[1]));
    }

    [Fact]
    public async Task IngestAsync_DoesNotStoreEmptyChunksAndClearsExistingSourceRecords()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "empty.md"), CreateDocument(string.Empty));
        var handler = new RecordingHandler();

        var result = await CreateService(directory.Path, handler, chunkSize: 10, overlapPercentage: 15).IngestAsync();

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.ChunksAddedOrUpdated);
        Assert.Empty(handler.UpsertBodies);
        Assert.Equal(["delete"], handler.Operations);
    }

    private static RagDocumentIngestionService CreateService(
        string knowledgeRoot,
        RecordingHandler handler,
        int chunkSize = 256,
        int overlapPercentage = 15)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8000/")
        };
        var chroma = new ChromaClient(httpClient, Options.Create(new ChromaOptions
        {
            BaseUrl = "http://localhost:8000",
            CollectionName = "cfo-financial-knowledge",
            Tenant = "default_tenant",
            Database = "default_database",
            TimeoutSeconds = 10
        }));
        IEmbeddingGenerator<string, Embedding<float>> embeddings = new DeterministicTokenHashEmbeddingGenerator();

        return new RagDocumentIngestionService(chroma, embeddings, Options.Create(new RagOptions
        {
            KnowledgeFilesRoot = knowledgeRoot,
            MaxChunkCharacters = chunkSize,
            ChunkOverlapPercentage = overlapPercentage
        }));
    }

    private static string[] ReadDocuments(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("documents").EnumerateArray()
            .Select(value => value.GetString())
            .OfType<string>()
            .ToArray();
    }

    private static int[] ReadOffsets(string body, string property)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("metadatas").EnumerateArray()
            .Select(value => int.Parse(value.GetProperty(property).GetString()!, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static string[] ReadIds(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("ids").EnumerateArray()
            .Select(value => value.GetString())
            .OfType<string>()
            .ToArray();
    }

    private static string CreateDocument(string body) => $$"""
        ---
        document_id: test-document
        document_name: Test Document
        document_type: test_document
        period: 2026
        section: Test Section
        source_path: data/knowledge/test.md
        ---

        {{body}}
        """;

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> UpsertBodies { get; } = [];

        public List<string> DeleteBodies { get; } = [];

        public List<string> Operations { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/collections", StringComparison.Ordinal))
            {
                return JsonResponse("""{"id":"collection-1","name":"cfo-financial-knowledge"}""");
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/upsert", StringComparison.Ordinal))
            {
                Operations.Add("upsert");
                UpsertBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/delete", StringComparison.Ordinal))
            {
                Operations.Add("delete");
                DeleteBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            throw new InvalidOperationException($"Unexpected Chroma request: {request.RequestUri}");
        }

        private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cfo-agent-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private const string ValidDocument = """
        ---
        document_id: budget-2026
        document_name: Current Budget
        document_type: budget_target
        period: 2026
        section: annual-target
        source_path: data/knowledge/budget.md
        ---

        # Current Budget

        ## Annual Target

        The annual sales target is 3000000.

        ## Assumptions

        Pricing remains stable across all regions.
        """;
}
