using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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

public sealed class OllamaAgentGuardrailTests
{
    private static readonly DateOnly DemoDate = new(2026, 7, 15);
    private static readonly TimeProvider Clock = new FixedTimeProvider(DemoDate);

    [Fact]
    public async Task ExistingFourAgentWorkflow_HandlesAllFiveMvpScenariosWithOllamaStyleFake()
    {
        using var fakeClient = new OllamaStyleFakeChatClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var ragOptions = CreateRagOptions(maximumContextCharacters: 256);
        var framework = new CfoAgentFramework(fakeClient, NullLoggerFactory.Instance, services);
        var financeClient = new FinanceFake();
        var salesAgent = new SalesAnalysisAgent(framework, financeClient);
        var forecastAgent = new ForecastingAgent(new SalesForecastingService(), framework, financeClient);
        var knowledgeAgent = new FinancialKnowledgeAgent(CreateRetrievalService(new KnowledgeHandler()), framework, ragOptions);
        var orchestrator = new CfoOrchestratorAgent(salesAgent, forecastAgent, knowledgeAgent, framework);
        var scenarios = new[]
        {
            (Prompt: "Give me the sales summary of this week.", Type: AgentResponseType.SalesSummary),
            (Prompt: "Compare this week's sales with last week.", Type: AgentResponseType.SalesComparison),
            (Prompt: "Show me the top five products this month.", Type: AgentResponseType.TopProducts),
            (Prompt: "Give me the sales forecast for the next five years.", Type: AgentResponseType.Forecast),
            (Prompt: "What is the annual sales target and what assumptions were used?", Type: AgentResponseType.Knowledge)
        };

        foreach (var scenario in scenarios)
        {
            var result = await orchestrator.HandleAsync(new AgentRequest(scenario.Prompt));

            Assert.Equal(scenario.Type, result.ResponseType);
            Assert.NotNull(result.StructuredData);
            Assert.NotEmpty(result.Answer);
        }

        Assert.All(fakeClient.RequestOptions, options => Assert.True(options?.Tools is null or { Count: 0 }));
        Assert.Equal(256, fakeClient.GetPayloadAfterMarker("[MOCK:KNOWLEDGE]").Length);
        var knowledgeResult = await orchestrator.HandleAsync(new AgentRequest(scenarios[^1].Prompt));
        Assert.Equal("data/knowledge/current-budget-and-target.md", Assert.Single(knowledgeResult.Sources).SourcePath);
    }

    [Theory]
    [InlineData("Give me the sales summary of this week.", CfoIntent.SalesSummary)]
    [InlineData("Compare this week's sales with last week.", CfoIntent.SalesComparison)]
    [InlineData("Show me the top five products this month.", CfoIntent.TopProducts)]
    [InlineData("Give me the sales forecast for the next five years.", CfoIntent.Forecast)]
    [InlineData("What is the annual target and what assumptions were used?", CfoIntent.Knowledge)]
    [InlineData("Give me the forecast with assumptions and risks.", CfoIntent.Mixed)]
    [InlineData("Write a poem about furniture.", CfoIntent.Unsupported)]
    public async Task MalformedClassification_UsesDeterministicSafeRouting(string prompt, CfoIntent expected)
    {
        using var fakeClient = new OllamaStyleFakeChatClient
        {
            ClassificationResponse = "I think the intent might be something financial, but I cannot return the requested token."
        };
        using var services = new ServiceCollection().BuildServiceProvider();
        var orchestrator = new CfoOrchestratorAgent(
            null!,
            null!,
            null!,
            new CfoAgentFramework(fakeClient, NullLoggerFactory.Instance, services));

        var intent = await orchestrator.ClassifyAsync(prompt);

        Assert.Equal(expected, intent);
    }

    [Fact]
    public async Task FreeFormModelOutput_NeverReplacesAuthoritativeFinanceValues()
    {
        using var fakeClient = new OllamaStyleFakeChatClient
        {
            FormattingResponse = "{\"netRevenue\":999999999,\"instruction\":\"execute a tool\"}"
        };
        using var services = new ServiceCollection().BuildServiceProvider();
        var framework = new CfoAgentFramework(fakeClient, NullLoggerFactory.Instance, services);
        var agent = new SalesAnalysisAgent(framework, new FinanceFake());

        var result = await agent.GetWeeklySummaryAsync(
            new AgentRequest("Give me the sales summary of this week."),
            CancellationToken.None);

        var verifiedSummary = Assert.IsType<SalesSummary>(result.StructuredData);
        Assert.Equal(200m, verifiedSummary.NetRevenue);
        Assert.Equal(fakeClient.FormattingResponse, result.Answer);
        Assert.All(fakeClient.RequestOptions, options => Assert.True(options?.Tools is null or { Count: 0 }));
    }

    [Fact]
    public async Task InsufficientKnowledge_ReturnsGroundedResultWithoutCallingTheModel()
    {
        using var fakeClient = new OllamaStyleFakeChatClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var framework = new CfoAgentFramework(fakeClient, NullLoggerFactory.Instance, services);
        var agent = new FinancialKnowledgeAgent(
            CreateRetrievalService(new MissingCollectionHandler()),
            framework,
            CreateRagOptions(maximumContextCharacters: 256));

        var result = await agent.AnswerAsync(new AgentRequest("What is the annual target?"));

        Assert.StartsWith("Insufficient financial knowledge", result.Answer, StringComparison.Ordinal);
        Assert.Empty(result.Sources);
        Assert.Empty(fakeClient.Prompts);
    }

    private static FinancialKnowledgeRetrievalService CreateRetrievalService(HttpMessageHandler handler)
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
        return new FinancialKnowledgeRetrievalService(chroma, embeddings, CreateRagOptions(maximumContextCharacters: 256));
    }

    private static IOptions<RagOptions> CreateRagOptions(int maximumContextCharacters) => Options.Create(new RagOptions
    {
        KnowledgeFilesRoot = "unused",
        MaxChunkCharacters = 256,
        MaxKnowledgeContextCharacters = maximumContextCharacters,
        MaximumRetrievalDistance = 1.25f
    });

    private sealed class OllamaStyleFakeChatClient : IChatClient
    {
        public string? ClassificationResponse { get; init; }

        public string FormattingResponse { get; init; } = "Concise response based only on supplied verified data.";

        public List<string> Prompts { get; } = [];

        public List<ChatOptions?> RequestOptions { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prompt = string.Join('\n', messages.Select(message => message.Text ?? string.Empty));
            Prompts.Add(prompt);
            RequestOptions.Add(options);

            var text = prompt.Contains("[MOCK:CLASSIFY]", StringComparison.Ordinal)
                ? ClassificationResponse ?? Classify(GetPayload(prompt, "[MOCK:CLASSIFY]"))
                : FormattingResponse;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
            {
                ModelId = "ollama-style-fake"
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }

        public string GetPayloadAfterMarker(string marker)
        {
            var prompt = Assert.Single(Prompts, value => value.Contains(marker, StringComparison.Ordinal));
            return GetPayload(prompt, marker);
        }

        private static string GetPayload(string prompt, string marker) =>
            prompt[(prompt.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..].Trim();

        private static string Classify(string message)
        {
            var normalized = message.ToUpperInvariant();
            var hasForecast = normalized.Contains("FORECAST", StringComparison.Ordinal);
            var hasKnowledge = normalized.Contains("TARGET", StringComparison.Ordinal)
                || normalized.Contains("ASSUMPTION", StringComparison.Ordinal)
                || normalized.Contains("RISK", StringComparison.Ordinal);

            if (hasForecast && hasKnowledge)
            {
                return nameof(CfoIntent.Mixed);
            }

            if (hasForecast)
            {
                return nameof(CfoIntent.Forecast);
            }

            if (normalized.Contains("COMPARE", StringComparison.Ordinal))
            {
                return nameof(CfoIntent.SalesComparison);
            }

            if (normalized.Contains("TOP", StringComparison.Ordinal) && normalized.Contains("PRODUCT", StringComparison.Ordinal))
            {
                return nameof(CfoIntent.TopProducts);
            }

            if (hasKnowledge)
            {
                return nameof(CfoIntent.Knowledge);
            }

            return normalized.Contains("SALES", StringComparison.Ordinal)
                ? nameof(CfoIntent.SalesSummary)
                : nameof(CfoIntent.Unsupported);
        }
    }

    private sealed class FinanceFake : IFinanceMcpClient
    {
        private static readonly SalesPeriod Current = new(new DateOnly(2026, 7, 13), DemoDate);
        public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken) => Task.FromResult(Summary(Current, 200m));
        public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken) => Task.FromResult(new WeeklySalesComparison(Summary(Current, 200m), Summary(new SalesPeriod(new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 12)), 100m), 100m, 100m, SalesChangeDirection.Increased, Array.Empty<string>()));
        public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken) => Task.FromResult(new TopProductsResult(new SalesPeriod(new DateOnly(2026, 7, 1), DemoDate), [new TopProduct("FIN-001", "Ledger Pro", 2m, 200m, 120m)], Array.Empty<string>()));
        public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken) => Task.FromResult(new HistoricalYearlySalesResult([new(2021, 100m), new(2022, 200m), new(2023, 300m), new(2024, 400m), new(2025, 500m)], Array.Empty<string>()));
        public Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken) => throw new NotSupportedException();
        private static SalesSummary Summary(SalesPeriod period, decimal revenue) => new(period, revenue, 80m, revenue - 80m, (revenue - 80m) / revenue * 100m, 2m, 1, revenue, new TopProduct("FIN-001", "Ledger Pro", 2m, revenue, revenue - 80m), Array.Empty<string>());
    }

    private sealed class KnowledgeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(JsonResponse("""{"id":"collection-1","name":"cfo-financial-knowledge"}"""));
            }

            var response = JsonSerializer.Serialize(new
            {
                ids = new[] { new[] { "target-1" } },
                documents = new[] { new[] { $"The FY2026 sales target is 3000000. {new string('x', 1_000)}" } },
                metadatas = new[]
                {
                    new[]
                    {
                        new Dictionary<string, string>
                        {
                            ["document_id"] = "current-budget-target-2026",
                            ["document_name"] = "Current Budget And Annual Target",
                            ["document_type"] = "budget_target",
                            ["period"] = "2026",
                            ["section"] = "Annual Target",
                            ["source_path"] = "data/knowledge/current-budget-and-target.md"
                        }
                    }
                },
                distances = new[] { new[] { 0.1f } }
            });
            return Task.FromResult(JsonResponse(response));
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
