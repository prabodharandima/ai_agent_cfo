using System.Net;
using System.Text;
using System.Text.Json;
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

namespace CfoAgent.Api.Tests.Mcp;

public sealed class AgentMcpWiringTests
{
    private static readonly DateOnly DemoDate = new(2026, 7, 15);
    private static readonly TimeProvider Clock = new FixedTimeProvider(DemoDate);

    [Fact]
    public async Task SalesAgentUsesFinanceMcpWhenEnabled()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var mcpSummary = CreateSummary(975m);
        var mcp = new StubFinanceMcpClient { Summary = _ => Task.FromResult(mcpSummary) };
        using var client = CreateMockClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateSalesAgent(database, client, services, mcp, financeEnabled: true);

        var result = await agent.GetWeeklySummaryAsync(new AgentRequest("Show this week's sales."), CancellationToken.None);

        Assert.Equal(1, mcp.SummaryCalls);
        Assert.Equal(mcpSummary, Assert.IsType<SalesSummary>(result.StructuredData));
    }

    [Fact]
    public async Task SalesAgentUsesLocalFinanceServiceWhenMcpIsDisabled()
    {
        await using var database = await CreateDatabaseWithCurrentSaleAsync(210m);
        var mcp = new StubFinanceMcpClient { Summary = _ => Task.FromResult(CreateSummary(975m)) };
        using var client = CreateMockClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateSalesAgent(database, client, services, mcp, financeEnabled: false);

        var result = await agent.GetWeeklySummaryAsync(new AgentRequest("Show this week's sales."), CancellationToken.None);

        Assert.Equal(0, mcp.SummaryCalls);
        Assert.Equal(210m, Assert.IsType<SalesSummary>(result.StructuredData).NetRevenue);
    }

    [Fact]
    public async Task SalesAgentUsesLocalFinanceServiceWhenMcpFails()
    {
        await using var database = await CreateDatabaseWithCurrentSaleAsync(320m);
        var mcp = new StubFinanceMcpClient
        {
            Summary = _ => throw new InvalidOperationException("Finance MCP unavailable.")
        };
        using var client = CreateMockClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateSalesAgent(database, client, services, mcp, financeEnabled: true);

        var result = await agent.GetWeeklySummaryAsync(new AgentRequest("Show this week's sales."), CancellationToken.None);

        Assert.Equal(1, mcp.SummaryCalls);
        Assert.Equal(320m, Assert.IsType<SalesSummary>(result.StructuredData).NetRevenue);
    }

    [Fact]
    public async Task ForecastingAgentUsesMcpHistoricalDataWhenEnabled()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var historical = CreateHistoricalTotals();
        var mcp = new StubFinanceMcpClient { Historical = _ => Task.FromResult(historical) };
        using var client = CreateMockClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateForecastingAgent(database, client, services, mcp, financeEnabled: true);

        var result = await agent.GetForecastAsync(new AgentRequest("Forecast sales."), CancellationToken.None);

        var forecast = Assert.IsType<SalesForecastResult>(result.StructuredData);
        Assert.Equal(1, mcp.HistoricalCalls);
        Assert.Equal(historical.Totals, forecast.HistoricalInputs);
        Assert.Equal(600m, forecast.Forecasts[0].ExpectedNetRevenue);
    }

    [Fact]
    public async Task ForecastingAgentKeepsCalculationsInDeterministicCodeBeforeMockFormatting()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var mcp = new StubFinanceMcpClient { Historical = _ => Task.FromResult(CreateHistoricalTotals()) };
        using var client = CreateMockClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateForecastingAgent(database, client, services, mcp, financeEnabled: true);

        var result = await agent.GetForecastAsync(new AgentRequest("Forecast sales."), CancellationToken.None);

        var forecast = Assert.IsType<SalesForecastResult>(result.StructuredData);
        Assert.Equal([600m, 700m, 800m, 900m, 1000m], forecast.Forecasts.Select(row => row.ExpectedNetRevenue));
        Assert.Equal(JsonSerializer.Serialize(forecast), result.Answer.Split('\n', 2)[1]);
        Assert.Contains(forecast.Assumptions, assumption => assumption.Contains("do not include an LLM", StringComparison.Ordinal));
    }

    [Fact]
    public async Task KnowledgeAgentUsesDirectRagWhenFileMcpIsDisabled()
    {
        var handler = new KnowledgeHandler();
        var fileMcp = new StubKnowledgeFileMcpClient();
        using var client = CreateMockClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateKnowledgeAgent(handler, client, services, fileMcp, knowledgeEnabled: false);

        var result = await agent.AnswerAsync(new AgentRequest("What is the annual target?"));

        Assert.Equal(0, fileMcp.ListCalls);
        Assert.Equal(1, handler.QueryCalls);
        Assert.Equal("data/knowledge/current-budget-and-target.md", Assert.Single(result.Sources).SourcePath);
    }

    [Fact]
    public async Task KnowledgeAgentPreservesDirectRagResultsAfterFileMcpFallback()
    {
        var handler = new KnowledgeHandler();
        var fileMcp = new StubKnowledgeFileMcpClient();
        using var client = CreateMockClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateKnowledgeAgent(handler, client, services, fileMcp, knowledgeEnabled: true);

        var result = await agent.AnswerAsync(new AgentRequest("What is the annual target?"));

        Assert.Equal(1, fileMcp.ListCalls);
        Assert.Equal(1, handler.QueryCalls);
        Assert.Equal("data/knowledge/current-budget-and-target.md", Assert.Single(result.Sources).SourcePath);
    }

    [Fact]
    public async Task CallerCancellationIsPropagatedWithoutFinanceFallback()
    {
        await using var database = await CreateDatabaseWithCurrentSaleAsync(450m);
        var mcp = new StubFinanceMcpClient
        {
            Summary = async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return CreateSummary(975m);
            }
        };
        using var client = CreateMockClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateSalesAgent(database, client, services, mcp, financeEnabled: true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            agent.GetWeeklySummaryAsync(new AgentRequest("Show this week's sales."), cancellation.Token));

        Assert.Equal(1, mcp.SummaryCalls);
    }

    [Fact]
    public async Task FinancialAgentsGiveMockLlmOnlyPrecalculatedStructuredValues()
    {
        await using var database = await TemporaryFinanceDatabase.CreateAsync();
        var mcpSummary = CreateSummary(1234m);
        var mcp = new StubFinanceMcpClient { Summary = _ => Task.FromResult(mcpSummary) };
        using var client = CreateMockClient();
        using var services = new ServiceCollection().BuildServiceProvider();
        var agent = CreateSalesAgent(database, client, services, mcp, financeEnabled: true);

        var result = await agent.GetWeeklySummaryAsync(new AgentRequest("Show this week's sales."), CancellationToken.None);

        Assert.Equal(JsonSerializer.Serialize(mcpSummary), result.Answer.Split('\n', 2)[1]);
        Assert.Equal(1234m, Assert.IsType<SalesSummary>(result.StructuredData).NetRevenue);
    }

    private static SalesAnalysisAgent CreateSalesAgent(
        TemporaryFinanceDatabase database,
        MockChatClient client,
        IServiceProvider services,
        IFinanceMcpClient mcp,
        bool financeEnabled)
    {
        var sales = new SalesAnalysisService(database.Context, Clock);
        return new SalesAnalysisAgent(
            sales,
            new CfoAgentFramework(client, NullLoggerFactory.Instance, services),
            mcp,
            CreateFinanceFallback(financeEnabled));
    }

    private static ForecastingAgent CreateForecastingAgent(
        TemporaryFinanceDatabase database,
        MockChatClient client,
        IServiceProvider services,
        IFinanceMcpClient mcp,
        bool financeEnabled)
    {
        var sales = new SalesAnalysisService(database.Context, Clock);
        return new ForecastingAgent(
            new SalesForecastingService(sales, Clock),
            new CfoAgentFramework(client, NullLoggerFactory.Instance, services),
            mcp,
            CreateFinanceFallback(financeEnabled));
    }

    private static FinancialKnowledgeAgent CreateKnowledgeAgent(
        HttpMessageHandler handler,
        MockChatClient client,
        IServiceProvider services,
        IKnowledgeFileMcpClient fileMcp,
        bool knowledgeEnabled)
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
        var ragOptions = Options.Create(new RagOptions
        {
            KnowledgeFilesRoot = "unused",
            MaxChunkCharacters = 256,
            MaxKnowledgeContextCharacters = 4000,
            MaximumRetrievalDistance = 1.25f
        });
        var retrieval = new FinancialKnowledgeRetrievalService(chroma, embeddings, ragOptions);

        return new FinancialKnowledgeAgent(
            retrieval,
            new CfoAgentFramework(client, NullLoggerFactory.Instance, services),
            ragOptions,
            knowledgeEnabled ? fileMcp : null);
    }

    private static FinanceMcpFallback CreateFinanceFallback(bool enabled) => new(
        Options.Create(CreateMcpOptions(financeEnabled: enabled, knowledgeEnabled: false)),
        NullLogger<FinanceMcpFallback>.Instance);

    private static McpOptions CreateMcpOptions(bool financeEnabled, bool knowledgeEnabled) => new()
    {
        UseLocalFallback = true,
        Finance = new FinanceMcpOptions { Enabled = financeEnabled, ServerProjectPath = "unused", TimeoutSeconds = 1 },
        KnowledgeFiles = new KnowledgeFileMcpOptions { Enabled = knowledgeEnabled, RootPath = "unused", TimeoutSeconds = 1 }
    };

    private static async Task<TemporaryFinanceDatabase> CreateDatabaseWithCurrentSaleAsync(decimal revenue)
    {
        var database = await TemporaryFinanceDatabase.CreateAsync();
        var product = await database.AddProductAsync("FIN-001", "Ledger Pro");
        database.AddSale(product, "CURRENT", DemoDate, 1, revenue, 0m, 40m);
        await database.SaveChangesAsync();
        return database;
    }

    private static SalesSummary CreateSummary(decimal revenue) => new(
        new SalesPeriod(new DateOnly(2026, 7, 13), DemoDate),
        revenue,
        40m,
        revenue - 40m,
        (revenue - 40m) / revenue * 100m,
        1m,
        1,
        revenue,
        new TopProduct("FIN-001", "Ledger Pro", 1m, revenue, revenue - 40m),
        Array.Empty<string>());

    private static HistoricalYearlySalesResult CreateHistoricalTotals() => new(
        [
            new YearlySalesTotal(2021, 100m),
            new YearlySalesTotal(2022, 200m),
            new YearlySalesTotal(2023, 300m),
            new YearlySalesTotal(2024, 400m),
            new YearlySalesTotal(2025, 500m)
        ],
        Array.Empty<string>());

    private static MockChatClient CreateMockClient() => new(Options.Create(new AiOptions
    {
        Provider = "Mock",
        Model = "DeterministicMock"
    }));

    private sealed class StubFinanceMcpClient : IFinanceMcpClient
    {
        public Func<CancellationToken, Task<SalesSummary>> Summary { get; init; } = _ => throw new NotSupportedException();

        public Func<CancellationToken, Task<HistoricalYearlySalesResult>> Historical { get; init; } = _ => throw new NotSupportedException();

        public int SummaryCalls { get; private set; }

        public int HistoricalCalls { get; private set; }

        public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken)
        {
            SummaryCalls++;
            return Summary(cancellationToken);
        }

        public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken)
        {
            HistoricalCalls++;
            return Historical(cancellationToken);
        }

        public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubKnowledgeFileMcpClient : IKnowledgeFileMcpClient
    {
        public Func<CancellationToken, Task<IReadOnlyList<string>>> List { get; init; } =
            _ => Task.FromResult<IReadOnlyList<string>>(["current-budget-and-target.md"]);

        public int ListCalls { get; private set; }

        public Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken)
        {
            ListCalls++;
            return List(cancellationToken);
        }

        public Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class KnowledgeHandler : HttpMessageHandler
    {
        public int QueryCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(JsonResponse("""{"id":"collection-1","name":"cfo-financial-knowledge"}"""));
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/query", StringComparison.Ordinal))
            {
                QueryCalls++;
                return Task.FromResult(JsonResponse("""
                    {
                      "ids":[["target-1"]],
                      "documents":[["The FY2026 sales target is 3000000."]],
                      "metadatas":[[{
                        "document_id":"current-budget-target-2026",
                        "document_name":"Current Budget And Annual Target",
                        "document_type":"budget_target",
                        "period":"2026",
                        "section":"Annual Target",
                        "source_path":"data/knowledge/current-budget-and-target.md"
                      }]],
                      "distances":[[0.1]]
                    }
                    """));
            }

            throw new InvalidOperationException($"Unexpected Chroma request: {request.RequestUri}");
        }
    }

    private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };
}
