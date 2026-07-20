using System.Net;
using System.Text;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Chroma;
using CfoAgent.Api.Rag.Embeddings;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using CfoAgent.Api.Tests.AI;

namespace CfoAgent.Api.Tests.Rag.Retrieval;

public sealed class FinancialKnowledgeRetrievalTests
{
    [Fact]
    public async Task RetrieveAsync_MapsFiltersAndDeduplicatesRelevantSources()
    {
        var service = CreateSearch(new KnowledgeHandler());

        var result = await service.RetrieveAsync(new FinancialKnowledgeQuery(
            "What is the annual target and which assumptions were used?",
            TopK: 3,
            DocumentType: "budget_target",
            Period: "2026"));

        var source = Assert.Single(result.Sources);
        Assert.True(result.HasSufficientKnowledge);
        Assert.Equal("target-2", source.ChunkId);
        Assert.Equal("Current Budget And Annual Target", source.DocumentName);
        Assert.Equal("Annual Target", source.Section);
        Assert.Equal("data/knowledge/current-budget-and-target.md", source.SourcePath);
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsExplicitInsufficientKnowledgeWhenCollectionIsMissing()
    {
        var service = CreateSearch(new MissingCollectionHandler());

        var result = await service.RetrieveAsync(new FinancialKnowledgeQuery("What is the annual target?"));

        Assert.False(result.HasSufficientKnowledge);
        Assert.Empty(result.Sources);
        Assert.Contains("has been ingested", Assert.Single(result.Warnings), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinancialKnowledgeAgent_AnswersFromRetrievedContextWithUniqueCitations()
    {
        var retrieval = CreateSearch(new KnowledgeHandler());
        using var client = TestChatClient.CreateMvp();
        var agent = new FinancialKnowledgeAgent(
            retrieval,
            client,
            Options.Create(new RagOptions { KnowledgeFilesRoot = "unused", MaxChunkCharacters = 256, MaxKnowledgeContextCharacters = 4000 }));

        var result = await agent.AnswerAsync(new AgentRequest("What is the annual target and what assumptions were used?"));

        Assert.Equal(AgentResponseType.Knowledge, result.ResponseType);
        Assert.Equal(AgentDefinitions.FinancialKnowledge.Name, Assert.Single(result.AgentNames));
        Assert.Contains("Verified test response", result.Answer, StringComparison.Ordinal);
        Assert.Equal(4, result.Sources.Count);
        Assert.Contains(result.Sources, source => source.DocumentName == "Current Budget And Annual Target");
        Assert.Contains(result.Sources, source => source.DocumentName == "Forecast Assumptions");
        Assert.Contains(result.Sources, source => source.DocumentName == "Market Risks");
        Assert.Contains(result.Sources, source => source.DocumentName == "Product Strategy");
        Assert.Contains(result.Sources, source => source.Period == "2026");
    }

    [Fact]
    public async Task FinancialKnowledgeAgent_DoesNotFabricateWhenKnowledgeIsMissing()
    {
        var retrieval = CreateSearch(new MissingCollectionHandler());
        using var client = TestChatClient.CreateMvp();
        var agent = new FinancialKnowledgeAgent(
            retrieval,
            client,
            Options.Create(new RagOptions { KnowledgeFilesRoot = "unused", MaxChunkCharacters = 256, MaxKnowledgeContextCharacters = 4000 }));

        var result = await agent.AnswerAsync(new AgentRequest("What is the annual target?"));

        Assert.StartsWith("Insufficient financial knowledge", result.Answer);
        Assert.Empty(result.Sources);
        Assert.Contains("has been ingested", Assert.Single(result.Warnings), StringComparison.OrdinalIgnoreCase);
    }

    private static ChromaFinancialKnowledgeSearch CreateSearch(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000/") };
        var chroma = new ChromaClient(httpClient, Options.Create(new ChromaOptions
        {
            BaseUrl = "http://localhost:8000",
            CollectionName = "cfo-financial-knowledge",
            Tenant = "default_tenant",
            Database = "default_database",
            TimeoutSeconds = 10
        }));
        IEmbeddingGenerator<string, Embedding<float>> embeddings = new DeterministicTokenHashEmbeddingGenerator();
        var options = Options.Create(new RagOptions
        {
            KnowledgeFilesRoot = "unused",
            MaxChunkCharacters = 256,
            MaxKnowledgeContextCharacters = 4000,
            MaximumRetrievalDistance = 1.25f
        });

        return new ChromaFinancialKnowledgeSearch(chroma, embeddings, options);
    }

    private sealed class KnowledgeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(JsonResponse("""{"id":"collection-1","name":"cfo-financial-knowledge"}"""));
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/query", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse("""
                    {
                      "ids":[["target-1","target-2","assumptions-1","risks-1","strategy-1"]],
                      "documents":[["The FY2026 sales target is 3000000.","The FY2026 sales target is 3000000.","Expected results assume stable unit economics.","Discount pressure can reduce gross margin.","Prioritize Ledger Pro for enterprise expansion."]],
                      "metadatas":[[
                        {"document_id":"current-budget-target-2026","document_name":"Current Budget And Annual Target","document_type":"budget_target","period":"2026","section":"Annual Target","source_path":"data/knowledge/current-budget-and-target.md"},
                        {"document_id":"current-budget-target-2026","document_name":"Current Budget And Annual Target","document_type":"budget_target","period":"2026","section":"Annual Target","source_path":"data/knowledge/current-budget-and-target.md"},
                        {"document_id":"forecast-assumptions-2026","document_name":"Forecast Assumptions","document_type":"forecast_assumptions","period":"2026-2030","section":"Planning Assumptions","source_path":"data/knowledge/forecast-assumptions.md"},
                        {"document_id":"market-risks-2026","document_name":"Market Risks","document_type":"market_risks","period":"2026","section":"Key Risks","source_path":"data/knowledge/market-risks.md"},
                        {"document_id":"product-strategy-2026","document_name":"Product Strategy","document_type":"product_strategy","period":"2026","section":"Growth Priorities","source_path":"data/knowledge/product-strategy.md"}
                      ]],
                      "distances":[[0.2,0.1,0.3,0.4,0.5]]
                    }
                    """));
            }

            throw new InvalidOperationException($"Unexpected Chroma request: {request.RequestUri}");
        }
    }

    private sealed class MissingCollectionHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };
}
