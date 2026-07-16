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

    private static RagDocumentIngestionService CreateService(string knowledgeRoot, RecordingHandler handler)
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
            MaxChunkCharacters = 256
        }));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> UpsertBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/collections", StringComparison.Ordinal))
            {
                return JsonResponse("""{"id":"collection-1","name":"cfo-financial-knowledge"}""");
            }

            if (request.RequestUri.AbsolutePath.EndsWith("/upsert", StringComparison.Ordinal))
            {
                UpsertBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
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
