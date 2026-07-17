using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CfoAgent.Api.Rag.Chroma;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CfoAgent.Api.Tests.Api;

public sealed class ChatApiTests : IClassFixture<ChatApiFactory>
{
    private readonly ChatApiFactory _factory;

    public ChatApiTests(ChatApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("Give me the sales summary of this week.", "sales_summary")]
    [InlineData("Compare this week's sales with last week.", "sales_comparison")]
    [InlineData("Show me the top five products this month.", "top_products")]
    [InlineData("Give me the sales forecast for the next five years.", "forecast")]
    [InlineData("What is the annual sales target and what assumptions were used?", "knowledge")]
    public async Task PostChat_HandlesEachMvpPromptOverHttp(string message, string responseType)
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/chat", new { message });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(responseBody);
        var body = document.RootElement;
        Assert.Equal(responseType, body.GetProperty("responseType").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("conversationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("answer").GetString()));
        Assert.Contains("CfoOrchestratorAgent", body.GetProperty("agentNames").EnumerateArray().Select(value => value.GetString()));
        Assert.True(body.TryGetProperty("structuredData", out _));
        Assert.True(body.TryGetProperty("sources", out _));
        Assert.True(body.TryGetProperty("assumptions", out _));
        Assert.True(body.TryGetProperty("warnings", out _));
        Assert.True(body.TryGetProperty("dataPeriod", out _));
        Assert.Equal("Mock", body.GetProperty("model").GetProperty("provider").GetString());
        Assert.DoesNotContain("CfoAgent.Api.Models", responseBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"message\":\"   \"}")]
    [InlineData("{}")]
    public async Task PostChat_ReturnsValidationProblemForInvalidRequests(string requestBody)
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/api/chat", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("message", out _));
    }

    [Fact]
    public async Task PostChat_ReturnsValidationProblemForAnOversizedMessage()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/chat", new { message = new string('x', 4_001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PostChat_ReturnsControlledProblemDetailsWhenTheAgentFails()
    {
        await using var factory = ChatApiFactory.CreateFailing();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/chat", new { message = "Give me the sales summary of this week." });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("temporarily unavailable", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Mock chat failure", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostChat_ReturnsTheCallerCorrelationIdWithoutEchoingThePrompt()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new { message = "Give me the sales summary of this week." })
        };
        request.Headers.Add("X-Correlation-ID", "phase-6-test-001");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("phase-6-test-001", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
    }

    [Fact]
    public async Task ReadyHealthEndpointReportsSqliteChromaAndMcpConfiguration()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var dependencies = document.RootElement.GetProperty("dependencies").EnumerateArray()
            .Select(dependency => dependency.GetProperty("name").GetString())
            .ToArray();
        Assert.Contains("sqlite", dependencies);
        Assert.Contains("chroma", dependencies);
        Assert.Contains("mcp", dependencies);
    }
}

public sealed class ChatApiFactory : WebApplicationFactory<Program>
{
    private readonly bool _simulateMockFailure;

    public ChatApiFactory()
        : this(simulateMockFailure: false)
    {
    }

    private ChatApiFactory(bool simulateMockFailure)
    {
        _simulateMockFailure = simulateMockFailure;
    }

    public static ChatApiFactory CreateFailing() => new(simulateMockFailure: true);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var repositoryRoot = FindRepositoryRoot();
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:ConnectionString", $"Data Source={Path.Combine(repositoryRoot, "data", "cfo-agent.db")}");
        builder.UseSetting("Rag:KnowledgeFilesRoot", Path.Combine(repositoryRoot, "data", "knowledge"));
        builder.UseSetting("Mcp:Finance:Enabled", "false");
        builder.UseSetting("Mcp:KnowledgeFiles:Enabled", "false");
        builder.UseSetting("AI:SimulateFailure", _simulateMockFailure.ToString());
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<ChromaClient>()
                .ConfigurePrimaryHttpMessageHandler(static () => new KnowledgeHandler());
        });
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CfoAgent.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the CFO AI Agent repository root.");
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
                  "ids":[["target-1","assumptions-1"]],
                  "documents":[["The FY2026 sales target is 3000000.","Expected results assume stable unit economics."]],
                  "metadatas":[[
                    {"document_id":"current-budget-target-2026","document_name":"Current Budget And Annual Target","document_type":"budget_target","period":"2026","section":"Annual Target","source_path":"data/knowledge/current-budget-and-target.md"},
                    {"document_id":"forecast-assumptions-2026","document_name":"Forecast Assumptions","document_type":"forecast_assumptions","period":"2026-2030","section":"Planning Assumptions","source_path":"data/knowledge/forecast-assumptions.md"}
                  ]],
                  "distances":[[0.1,0.2]]
                }
                """));
        }

        private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }
}
