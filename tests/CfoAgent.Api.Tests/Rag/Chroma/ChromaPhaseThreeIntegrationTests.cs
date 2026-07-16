using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Chroma;
using CfoAgent.Api.Rag.Embeddings;
using CfoAgent.Api.Rag.Ingestion;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Rag.Chroma;

public sealed class ChromaPhaseThreeIntegrationTests
{
    [ChromaFact]
    public async Task IngestsDemoDocumentsAndRetrievesEachKnowledgeTopic()
    {
        var collectionName = $"cfo-phase-3-gate-{Guid.NewGuid():N}";
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:8000/") };
        var chroma = new ChromaClient(httpClient, Options.Create(new ChromaOptions
        {
            BaseUrl = "http://localhost:8000",
            CollectionName = collectionName,
            Tenant = "default_tenant",
            Database = "default_database",
            TimeoutSeconds = 5
        }));

        await chroma.HeartbeatAsync();

        IEmbeddingGenerator<string, Embedding<float>> embeddings = new DeterministicTokenHashEmbeddingGenerator();
        var options = Options.Create(new RagOptions
        {
            KnowledgeFilesRoot = FindKnowledgeDirectory(),
            MaxChunkCharacters = 1200,
            MaxKnowledgeContextCharacters = 4000,
            MaximumRetrievalDistance = 4f
        });
        var ingestion = new RagDocumentIngestionService(chroma, embeddings, options);

        var ingestionResult = await ingestion.IngestAsync();
        Assert.Equal(5, ingestionResult.Documents);
        Assert.Equal(0, ingestionResult.Failed);
        Assert.True(ingestionResult.ChunksAddedOrUpdated > 0);

        var retrieval = new FinancialKnowledgeRetrievalService(chroma, embeddings, options);
        await AssertTopicAsync(retrieval, "annual sales target", "budget_target", "Current Budget And Annual Target");
        await AssertTopicAsync(retrieval, "forecast planning assumptions", "forecast_assumptions", "Forecast Assumptions");
        await AssertTopicAsync(retrieval, "market discount risk", "market_risks", "Market Risks");
        await AssertTopicAsync(retrieval, "product growth priorities", "product_strategy", "Product Strategy");
    }

    private static async Task AssertTopicAsync(
        FinancialKnowledgeRetrievalService retrieval,
        string query,
        string documentType,
        string documentName)
    {
        var result = await retrieval.RetrieveAsync(new FinancialKnowledgeQuery(query, TopK: 10, DocumentType: documentType));

        Assert.True(result.HasSufficientKnowledge);
        Assert.Contains(result.Sources, source => source.DocumentName == documentName);
        var source = result.Sources.First(source => source.DocumentName == documentName);
        Assert.Equal(documentName, source.DocumentName);
        Assert.False(string.IsNullOrWhiteSpace(source.Section));
        Assert.False(string.IsNullOrWhiteSpace(source.Period));
        Assert.False(string.IsNullOrWhiteSpace(source.SourcePath));
    }

    private static string FindKnowledgeDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var knowledgeDirectory = Path.Combine(directory.FullName, "data", "knowledge");
            if (Directory.Exists(knowledgeDirectory))
            {
                return knowledgeDirectory;
            }
        }

        throw new DirectoryNotFoundException("The demo knowledge directory could not be located for the ChromaDB integration test.");
    }
}
