using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CfoAgent.KnowledgeFileMcpServer;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CfoAgent.Api.Tests.PhaseEight;

[Trait("Category", "ContainerIntegration")]
public sealed class ContainerIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] FinanceTools =
    [
        "compare_sales_periods",
        "get_budget_target",
        "get_historical_sales",
        "get_sales_summary",
        "get_top_products"
    ];

    [ContainerIntegrationFact]
    public async Task AllFiveMvpPromptsUseTheRealContainerDependencies()
    {
        using var client = CreateHttpClient("CFO_CONTAINER_API_BASE_URL");
        var scenarios = new (string Prompt, string ResponseType)[]
        {
            ("Give me the sales summary of this week.", "sales_summary"),
            ("Compare this week's sales with last week.", "sales_comparison"),
            ("Show me the top five products this month.", "top_products"),
            ("Give me the sales forecast for the next five years.", "forecast"),
            ("What is the annual sales target and what assumptions were used?", "knowledge")
        };

        foreach (var scenario in scenarios)
        {
            using var response = await client.PostAsJsonAsync("api/chat", new { message = scenario.Prompt });
            var body = await response.Content.ReadAsStringAsync();

            Assert.True(response.IsSuccessStatusCode, body);
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            Assert.Equal(scenario.ResponseType, root.GetProperty("responseType").GetString());
            Assert.Equal("Mock", root.GetProperty("model").GetProperty("provider").GetString());
            Assert.Equal("DeterministicMock", root.GetProperty("model").GetProperty("name").GetString());
            Assert.Contains(
                root.GetProperty("agentNames").EnumerateArray().Select(agent => agent.GetString()),
                agent => string.Equals(agent, "CfoOrchestratorAgent", StringComparison.Ordinal));
            Assert.Equal(JsonValueKind.Object, root.GetProperty("structuredData").ValueKind);

            if (scenario.ResponseType == "sales_summary")
            {
                Assert.True(root.GetProperty("structuredData").GetProperty("NetRevenue").GetDecimal() > 0m);
            }

            if (scenario.ResponseType == "knowledge")
            {
                var sources = root.GetProperty("sources").EnumerateArray().ToArray();
                Assert.NotEmpty(sources);
                Assert.All(sources, source =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(source.GetProperty("documentName").GetString()));
                    Assert.StartsWith("data/knowledge/", source.GetProperty("sourcePath").GetString(), StringComparison.Ordinal);
                });
            }
        }
    }

    [ContainerIntegrationFact]
    public async Task RealMcpServersExposeOnlyApprovedToolsAndExecuteReadOnlyCalls()
    {
        await using var financeClient = await CreateMcpClientAsync("CFO_CONTAINER_FINANCE_MCP_BASE_URL");
        await using var knowledgeClient = await CreateMcpClientAsync("CFO_CONTAINER_KNOWLEDGE_MCP_BASE_URL");

        var financeTools = (await financeClient.ListToolsAsync(cancellationToken: CancellationToken.None))
            .Select(tool => tool.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var knowledgeTools = (await knowledgeClient.ListToolsAsync(cancellationToken: CancellationToken.None))
            .Select(tool => tool.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(FinanceTools, financeTools);
        Assert.Equal(["list_knowledge_files", "read_knowledge_file"], knowledgeTools);

        var financeCalls = new (string Tool, IReadOnlyDictionary<string, object?> Arguments)[]
        {
            ("get_sales_summary", new Dictionary<string, object?> { ["startDate"] = "2026-07-13", ["endDate"] = "2026-07-15" }),
            ("compare_sales_periods", new Dictionary<string, object?>
            {
                ["currentStartDate"] = "2026-07-13",
                ["currentEndDate"] = "2026-07-15",
                ["previousStartDate"] = "2026-07-06",
                ["previousEndDate"] = "2026-07-12"
            }),
            ("get_top_products", new Dictionary<string, object?> { ["startDate"] = "2026-07-01", ["endDate"] = "2026-07-15", ["limit"] = 5 }),
            ("get_historical_sales", new Dictionary<string, object?> { ["startYear"] = 2021, ["endYear"] = 2025 }),
            ("get_budget_target", new Dictionary<string, object?> { ["year"] = 2026, ["month"] = null })
        };

        foreach (var call in financeCalls)
        {
            var result = await financeClient.CallToolAsync(call.Tool, call.Arguments, cancellationToken: CancellationToken.None);
            Assert.NotEqual(true, result.IsError);
            Assert.Contains("\"isSuccess\":true", GetText(result), StringComparison.Ordinal);
        }

        var files = await CallKnowledgeAsync<string[]>(knowledgeClient, "list_knowledge_files", null);
        var content = await CallKnowledgeAsync<string>(
            knowledgeClient,
            "read_knowledge_file",
            new Dictionary<string, object?> { ["relativePath"] = "product-strategy.md" });
        Assert.True(files.IsSuccess);
        Assert.Contains("product-strategy.md", files.Data ?? []);
        Assert.True(content.IsSuccess);
        Assert.Contains("document_id: product-strategy-2026", content.Data, StringComparison.Ordinal);

        foreach (var blockedPath in new[] { "../outside.md", "..\\outside.md", "/etc/passwd" })
        {
            var blocked = await knowledgeClient.CallToolAsync(
                "read_knowledge_file",
                new Dictionary<string, object?> { ["relativePath"] = blockedPath },
                cancellationToken: CancellationToken.None);
            Assert.True(blocked.IsError);
            Assert.DoesNotContain("/knowledge", GetText(blocked), StringComparison.OrdinalIgnoreCase);
        }
    }

    [ContainerIntegrationFact]
    public async Task CallerCancellationIsNotReturnedAsDependencyFailure()
    {
        using var client = CreateHttpClient("CFO_CONTAINER_API_BASE_URL");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await client.PostAsJsonAsync(
                "api/chat",
                new { message = "Give me the sales summary of this week." },
                cancellation.Token));

        using var readiness = await client.GetAsync("health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
    }

    private static HttpClient CreateHttpClient(string environmentVariable) => new()
    {
        BaseAddress = GetBaseUri(environmentVariable),
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static async Task<McpClient> CreateMcpClientAsync(string environmentVariable)
    {
        var httpClient = CreateHttpClient(environmentVariable);
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(GetBaseUri(environmentVariable), "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = TimeSpan.FromSeconds(10)
            },
            httpClient,
            ownsHttpClient: true);

        try
        {
            return await McpClient.CreateAsync(transport, cancellationToken: CancellationToken.None);
        }
        catch
        {
            await transport.DisposeAsync();
            throw;
        }
    }

    private static async Task<KnowledgeFileMcpResult<T>> CallKnowledgeAsync<T>(
        McpClient client,
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments)
    {
        var result = await client.CallToolAsync(toolName, arguments, cancellationToken: CancellationToken.None);
        Assert.NotEqual(true, result.IsError);
        return JsonSerializer.Deserialize<KnowledgeFileMcpResult<T>>(GetText(result), JsonOptions)
            ?? throw new JsonException($"Tool {toolName} returned an empty result.");
    }

    private static string GetText(CallToolResult result) =>
        string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    private static Uri GetBaseUri(string environmentVariable)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"{environmentVariable} must contain an absolute URI for the container gate.");
        }

        return uri;
    }
}
