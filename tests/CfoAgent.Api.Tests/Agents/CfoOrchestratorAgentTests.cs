using System.Net;
using System.Text;
using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Rag.Chroma;
using CfoAgent.Api.Rag.Embeddings;
using CfoAgent.Api.Rag.Retrieval;
using CfoAgent.Api.Tests.Finance;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Agents;

public sealed class CfoOrchestratorAgentTests
{
    [Theory]
    [InlineData("Give me the sales summary of this week.", CfoIntent.SalesSummary)]
    [InlineData("Compare this week's sales with last week.", CfoIntent.SalesComparison)]
    [InlineData("Show the top five products this month.", CfoIntent.TopProducts)]
    [InlineData("Give me the sales forecast for the next five years.", CfoIntent.Forecast)]
    [InlineData("What is the annual target and what assumptions were used?", CfoIntent.Knowledge)]
    public async Task ClassifyAsync_RecognizesEachMvpPrompt(string prompt, CfoIntent expected)
    {
        using var client = CreateClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var orchestrator = new CfoOrchestratorAgent(null!, null!, null!, new CfoAgentFramework(client, NullLoggerFactory.Instance, services));

        var intent = await orchestrator.ClassifyAsync(prompt);

        Assert.Equal(expected, intent);
    }

    [Fact]
    public async Task HandleAsync_CombinesForecastAndKnowledgeForForecastAssumptions()
    {
        using var client = CreateClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var framework = new CfoAgentFramework(client, NullLoggerFactory.Instance, services);
        var financeClient = new FinanceFake();
        var salesAgent = new SalesAnalysisAgent(framework, financeClient);
        var forecastAgent = new ForecastingAgent(
            new SalesForecastingService(),
            framework,
            financeClient);
        var knowledgeAgent = new FinancialKnowledgeAgent(CreateRetrievalService(), framework, CreateRagOptions());
        var orchestrator = new CfoOrchestratorAgent(salesAgent, forecastAgent, knowledgeAgent, framework);

        var result = await orchestrator.HandleAsync(new AgentRequest("Give me the sales forecast for the next five years with assumptions."));

        Assert.Equal(AgentResponseType.Mixed, result.ResponseType);
        Assert.Equal([AgentDefinitions.Forecasting.Name, AgentDefinitions.FinancialKnowledge.Name], result.AgentNames);
        Assert.Contains("Mock CFO orchestrated response", result.Answer, StringComparison.Ordinal);
        Assert.NotEmpty(result.Assumptions);
        Assert.Single(result.Sources);
    }

    [Fact]
    public async Task HandleAsync_ReturnsSafeScopedResponseForUnsupportedQuestions()
    {
        using var client = CreateClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var orchestrator = new CfoOrchestratorAgent(null!, null!, null!, new CfoAgentFramework(client, NullLoggerFactory.Instance, services));

        var result = await orchestrator.HandleAsync(new AgentRequest("Write a limerick about office furniture."));

        Assert.Equal(AgentResponseType.Unsupported, result.ResponseType);
        Assert.Contains("outside the supported CFO MVP scope", result.Answer, StringComparison.Ordinal);
    }

    private static FinancialKnowledgeRetrievalService CreateRetrievalService()
    {
        var httpClient = new HttpClient(new KnowledgeHandler()) { BaseAddress = new Uri("http://localhost:8000/") };
        var chroma = new ChromaClient(httpClient, Options.Create(new ChromaOptions
        {
            BaseUrl = "http://localhost:8000",
            CollectionName = "cfo-financial-knowledge",
            Tenant = "default_tenant",
            Database = "default_database",
            TimeoutSeconds = 10
        }));
        IEmbeddingGenerator<string, Embedding<float>> embeddings = new DeterministicTokenHashEmbeddingGenerator();
        return new FinancialKnowledgeRetrievalService(chroma, embeddings, CreateRagOptions());
    }

    private static IOptions<RagOptions> CreateRagOptions() => Options.Create(new RagOptions
    {
        KnowledgeFilesRoot = "unused",
        MaxChunkCharacters = 256,
        MaxKnowledgeContextCharacters = 4000,
        MaximumRetrievalDistance = 1.25f
    });

    private static MockChatClient CreateClient() => new(Options.Create(new AiOptions { Provider = "Mock", Model = "DeterministicMock" }));

    private sealed class FinanceFake : IFinanceMcpClient
    {
        public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken) => Task.FromResult(new HistoricalYearlySalesResult([new(2023, 100m), new(2024, 200m), new(2025, 300m)], Array.Empty<string>()));
        public Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class KnowledgeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(JsonResponse("""{"id":"collection-1","name":"cfo-financial-knowledge"}"""));
            }

            return Task.FromResult(JsonResponse("""
                {
                  "ids":[["assumptions-1"]],
                  "documents":[["Expected results assume stable unit economics."]],
                  "metadatas":[[{"document_id":"forecast-assumptions-2026","document_name":"Forecast Assumptions","document_type":"forecast_assumptions","period":"2026-2030","section":"Planning Assumptions","source_path":"data/knowledge/forecast-assumptions.md"}]],
                  "distances":[[0.1]]
                }
                """));
        }

        private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }
}
