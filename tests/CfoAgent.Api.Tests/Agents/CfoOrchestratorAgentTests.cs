using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Rag.Retrieval;
using CfoAgent.Api.Tests.Finance;
using Microsoft.Extensions.AI;
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
        var orchestrator = new CfoOrchestratorAgent(null!, null!, null!, new AgentResultComposer(), client);

        var intent = await orchestrator.ClassifyAsync(prompt);

        Assert.Equal(expected, intent);
    }

    [Theory]
    [InlineData("Give me the sales summary of this week.", CfoIntent.SalesSummary)]
    [InlineData("Compare this week's sales with last week.", CfoIntent.SalesComparison)]
    [InlineData("Show the top five products this month.", CfoIntent.TopProducts)]
    [InlineData("Give me the sales forecast for the next five years.", CfoIntent.Forecast)]
    [InlineData("What is the annual target and what assumptions were used?", CfoIntent.Knowledge)]
    [InlineData("Give me the forecast and target assumptions.", CfoIntent.Mixed)]
    [InlineData("Write a limerick about office furniture.", CfoIntent.Unsupported)]
    public async Task ClassifyAsync_DeterministicFallbackMatchesMockForEveryIntent(string prompt, CfoIntent expected)
    {
        using var mockClient = CreateClient();
        using var fallbackClient = new InvalidClassificationChatClient();
        var mockOrchestrator = new CfoOrchestratorAgent(null!, null!, null!, new AgentResultComposer(), mockClient);
        var fallbackOrchestrator = new CfoOrchestratorAgent(null!, null!, null!, new AgentResultComposer(), fallbackClient);

        var mockIntent = await mockOrchestrator.ClassifyAsync(prompt);
        var fallbackIntent = await fallbackOrchestrator.ClassifyAsync(prompt);

        Assert.Equal(expected, mockIntent);
        Assert.Equal(mockIntent, fallbackIntent);
    }

    [Fact]
    public async Task HandleAsync_CombinesForecastAndKnowledgeForForecastAssumptions()
    {
        using var client = CreateClient();
        var financeClient = new FinanceFake();
        var knowledgeSearch = new KnowledgeSearchFake();
        var salesAgent = new SalesAnalysisAgent(client, financeClient);
        var forecastAgent = new ForecastingAgent(
            new SalesForecastingService(),
            client,
            financeClient);
        var knowledgeAgent = new FinancialKnowledgeAgent(knowledgeSearch, client, CreateRagOptions());
        var orchestrator = new CfoOrchestratorAgent(salesAgent, forecastAgent, knowledgeAgent, new AgentResultComposer(), client);

        var result = await orchestrator.HandleAsync(new AgentRequest("Give me the sales forecast for the next five years with assumptions."));

        Assert.Equal(AgentResponseType.Mixed, result.ResponseType);
        Assert.Equal([AgentDefinitions.Forecasting.Name, AgentDefinitions.FinancialKnowledge.Name], result.AgentNames);
        Assert.StartsWith("Mock forecast explanation", result.Answer, StringComparison.Ordinal);
        Assert.Contains("Mock knowledge answer", result.Answer, StringComparison.Ordinal);
        Assert.NotEmpty(result.Assumptions);
        Assert.Single(result.Sources);
        Assert.Equal(1, financeClient.HistoricalCalls);
        Assert.Equal(1, knowledgeSearch.QueryCalls);
        Assert.Equal(2, Assert.IsType<OrchestratedSpecialistResult[]>(result.StructuredData).Length);
    }

    [Theory]
    [InlineData("Give me the sales summary of this week.", AgentResponseType.SalesSummary)]
    [InlineData("Compare this week's sales with last week.", AgentResponseType.SalesComparison)]
    [InlineData("Show the top five products this month.", AgentResponseType.TopProducts)]
    [InlineData("Give me the sales forecast for the next five years.", AgentResponseType.Forecast)]
    [InlineData("What is the annual target and what assumptions were used?", AgentResponseType.Knowledge)]
    public async Task HandleAsync_RoutesEachIntentToOnlyItsExpectedWorker(string prompt, AgentResponseType expectedType)
    {
        using var client = CreateClient();
        var financeClient = new FinanceFake();
        var knowledgeSearch = new KnowledgeSearchFake();
        var orchestrator = new CfoOrchestratorAgent(
            new SalesAnalysisAgent(client, financeClient),
            new ForecastingAgent(new SalesForecastingService(), client, financeClient),
            new FinancialKnowledgeAgent(knowledgeSearch, client, CreateRagOptions()),
            new AgentResultComposer(),
            client);

        var result = await orchestrator.HandleAsync(new AgentRequest(prompt));

        Assert.Equal(expectedType, result.ResponseType);
        Assert.Equal(expectedType == AgentResponseType.SalesSummary ? 1 : 0, financeClient.SummaryCalls);
        Assert.Equal(expectedType == AgentResponseType.SalesComparison ? 1 : 0, financeClient.ComparisonCalls);
        Assert.Equal(expectedType == AgentResponseType.TopProducts ? 1 : 0, financeClient.TopProductsCalls);
        Assert.Equal(expectedType == AgentResponseType.Forecast ? 1 : 0, financeClient.HistoricalCalls);
        Assert.Equal(expectedType == AgentResponseType.Knowledge ? 1 : 0, knowledgeSearch.QueryCalls);
    }

    [Fact]
    public async Task HandleAsync_PropagatesWorkerDependencyFailure()
    {
        using var client = CreateClient();
        var financeClient = new FinanceFake
        {
            SummaryFailure = new McpDependencyException("Finance MCP", McpDependencyFailureKind.Unavailable)
        };
        var orchestrator = new CfoOrchestratorAgent(
            new SalesAnalysisAgent(client, financeClient),
            null!,
            null!,
            new AgentResultComposer(),
            client);

        var exception = await Assert.ThrowsAsync<McpDependencyException>(() =>
            orchestrator.HandleAsync(new AgentRequest("Give me the sales summary of this week.")));

        Assert.Equal(McpDependencyFailureKind.Unavailable, exception.FailureKind);
    }

    [Fact]
    public async Task HandleAsync_PropagatesCallerCancellation()
    {
        using var client = CreateClient(simulatedDelayMilliseconds: 5_000);
        var orchestrator = new CfoOrchestratorAgent(
            null!,
            null!,
            null!,
            new AgentResultComposer(),
            client);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            orchestrator.HandleAsync(new AgentRequest("Give me the sales summary of this week."), cancellation.Token));
    }

    [Fact]
    public async Task HandleAsync_ReturnsSafeScopedResponseForUnsupportedQuestions()
    {
        using var client = CreateClient();
        var orchestrator = new CfoOrchestratorAgent(null!, null!, null!, new AgentResultComposer(), client);

        var result = await orchestrator.HandleAsync(new AgentRequest("Write a limerick about office furniture."));

        Assert.Equal(AgentResponseType.Unsupported, result.ResponseType);
        Assert.Contains("outside the supported CFO MVP scope", result.Answer, StringComparison.Ordinal);
    }

    private static IOptions<RagOptions> CreateRagOptions() => Options.Create(new RagOptions
    {
        KnowledgeFilesRoot = "unused",
        MaxChunkCharacters = 256,
        MaxKnowledgeContextCharacters = 4000,
        MaximumRetrievalDistance = 1.25f
    });

    private static MockChatClient CreateClient(int simulatedDelayMilliseconds = 0) => new(Options.Create(new AiOptions
    {
        Provider = "Mock",
        Model = "DeterministicMock",
        SimulatedDelayMilliseconds = simulatedDelayMilliseconds
    }));

    private sealed class InvalidClassificationChatClient : IChatClient
    {
        public ChatClientMetadata Metadata { get; } = new("Invalid", new Uri("https://invalid.local"), "invalid");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "not-an-intent")));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType == typeof(ChatClientMetadata) ? Metadata : null;

        public void Dispose()
        {
        }
    }

    private sealed class FinanceFake : IFinanceMcpClient
    {
        private static readonly SalesPeriod CurrentWeek = new(new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 15));

        public Exception? SummaryFailure { get; init; }
        public int SummaryCalls { get; private set; }
        public int ComparisonCalls { get; private set; }
        public int TopProductsCalls { get; private set; }
        public int HistoricalCalls { get; private set; }

        public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken)
        {
            SummaryCalls++;
            return SummaryFailure is null
                ? Task.FromResult(Summary(CurrentWeek, 200m))
                : Task.FromException<SalesSummary>(SummaryFailure);
        }

        public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken)
        {
            ComparisonCalls++;
            return Task.FromResult(new WeeklySalesComparison(
                Summary(CurrentWeek, 200m),
                Summary(new SalesPeriod(new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 12)), 100m),
                100m,
                100m,
                SalesChangeDirection.Increased,
                Array.Empty<string>()));
        }

        public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken)
        {
            TopProductsCalls++;
            return Task.FromResult(new TopProductsResult(
                new SalesPeriod(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 15)),
                [new TopProduct("FIN-001", "Ledger Pro", 2m, 200m, 120m)],
                Array.Empty<string>()));
        }

        public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken)
        {
            HistoricalCalls++;
            return Task.FromResult(new HistoricalYearlySalesResult([new(2023, 100m), new(2024, 200m), new(2025, 300m)], Array.Empty<string>()));
        }

        public Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken) => throw new NotSupportedException();

        private static SalesSummary Summary(SalesPeriod period, decimal revenue) => new(
            period,
            revenue,
            80m,
            revenue - 80m,
            (revenue - 80m) / revenue * 100m,
            2m,
            1,
            revenue,
            new TopProduct("FIN-001", "Ledger Pro", 2m, revenue, revenue - 80m),
            Array.Empty<string>());
    }

    private sealed class KnowledgeSearchFake : IFinancialKnowledgeSearch
    {
        public int QueryCalls { get; private set; }

        public Task<FinancialKnowledgeRetrievalResult> RetrieveAsync(
            FinancialKnowledgeQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QueryCalls++;
            return Task.FromResult(new FinancialKnowledgeRetrievalResult(
                [new FinancialKnowledgeSource(
                    "assumptions-1",
                    "forecast-assumptions-2026",
                    "Forecast Assumptions",
                    "forecast_assumptions",
                    "2026-2030",
                    "Planning Assumptions",
                    "data/knowledge/forecast-assumptions.md",
                    "Expected results assume stable unit economics.",
                    0.1f)],
                Array.Empty<string>()));
        }
    }
}
