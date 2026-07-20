using System.Net;
using System.Text;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Chroma;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Rag.Chroma;

public sealed class ChromaClientTests
{
    [Fact]
    public async Task GetOrCreateCollectionAsync_UsesConfiguredV2CollectionEndpoint()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""{"id":"collection-1","name":"financial-knowledge"}"""));
        var client = CreateClient(handler);

        var collection = await client.GetOrCreateCollectionAsync("financial-knowledge");

        Assert.Equal("collection-1", collection.Id);
        Assert.Equal("financial-knowledge", collection.Name);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "/api/v2/tenants/default_tenant/databases/default_database/collections",
            handler.PathAndQuery);
        Assert.Contains("\"get_or_create\":true", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpsertAsync_SendsDocumentsEmbeddingsAndMetadata()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);
        var collection = new ChromaCollection("collection-1", "financial-knowledge");
        var record = new ChromaRecord(
            "budget-1",
            "Budget target is 100.",
            [0.6f, 0.8f],
            new Dictionary<string, string> { ["source"] = "budget.md" });

        await client.UpsertAsync(collection, [record]);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "/api/v2/tenants/default_tenant/databases/default_database/collections/collection-1/upsert",
            handler.PathAndQuery);
        Assert.Contains("\"budget-1\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"Budget target is 100.\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"source\":\"budget.md\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteBySourcePathAsync_UsesMetadataFilterForTheConfiguredCollection()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.DeleteBySourcePathAsync(new ChromaCollection("collection-1", "financial-knowledge"), "data/knowledge/budget.md");

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "/api/v2/tenants/default_tenant/databases/default_database/collections/collection-1/delete",
            handler.PathAndQuery);
        Assert.Contains("\"source_path\":\"data/knowledge/budget.md\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_ParsesTopKMatches()
    {
        var handler = new RecordingHandler(_ => JsonResponse("""
            {
              "ids":[["budget-1"]],
              "documents":[["Budget target is 100."]],
              "metadatas":[[{"source":"budget.md"}]],
              "distances":[[0.12]]
            }
            """));
        var client = CreateClient(handler);

        var matches = await client.QueryAsync(new ChromaCollection("collection-1", "financial-knowledge"), [0.6f, 0.8f], 3);

        var match = Assert.Single(matches);
        Assert.Equal("budget-1", match.Id);
        Assert.Equal("Budget target is 100.", match.Document);
        Assert.Equal("budget.md", match.Metadata!["source"]);
        Assert.Equal(0.12f, match.Distance);
        Assert.Equal(
            "/api/v2/tenants/default_tenant/databases/default_database/collections/collection-1/query",
            handler.PathAndQuery);
    }

    [Fact]
    public async Task HeartbeatAsync_ConvertsDependencyFailuresToControlledException()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            ReasonPhrase = "Unavailable"
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ChromaDependencyException>(() => client.HeartbeatAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    private static ChromaClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8000/")
        };
        var options = Options.Create(new ChromaOptions
        {
            BaseUrl = "http://localhost:8000",
            CollectionName = "cfo-financial-knowledge",
            Tenant = "default_tenant",
            Database = "default_database",
            TimeoutSeconds = 10
        });

        return new ChromaClient(httpClient, options);
    }

    private static HttpResponseMessage JsonResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? PathAndQuery { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            PathAndQuery = request.RequestUri!.PathAndQuery;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
