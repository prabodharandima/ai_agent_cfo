using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Rag.Chroma;

public sealed class ChromaClient(HttpClient httpClient, IOptions<ChromaOptions> options)
{
    private readonly ChromaOptions _options = options.Value;

    public async Task HeartbeatAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, "api/v2/heartbeat"), cancellationToken);
        EnsureSuccess(response, "check ChromaDB heartbeat");
    }

    public async Task<ChromaCollection?> GetCollectionAsync(string? collectionName = null, CancellationToken cancellationToken = default)
    {
        var name = collectionName ?? _options.CollectionName;
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var response = await SendAsync(
            new HttpRequestMessage(HttpMethod.Get, $"{CollectionsPath}/{Uri.EscapeDataString(name)}"),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        EnsureSuccess(response, "get ChromaDB collection");
        return await ReadCollectionAsync(response, cancellationToken);
    }

    public async Task<ChromaCollection> CreateCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

        using var request = new HttpRequestMessage(HttpMethod.Post, CollectionsPath)
        {
            Content = JsonContent.Create(new { name = collectionName, get_or_create = false })
        };
        using var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response, "create ChromaDB collection");
        return await ReadCollectionAsync(response, cancellationToken);
    }

    public async Task<ChromaCollection> GetOrCreateCollectionAsync(string? collectionName = null, CancellationToken cancellationToken = default)
    {
        var name = collectionName ?? _options.CollectionName;
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var request = new HttpRequestMessage(HttpMethod.Post, CollectionsPath)
        {
            Content = JsonContent.Create(new { name, get_or_create = true })
        };
        using var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response, "get or create ChromaDB collection");
        return await ReadCollectionAsync(response, cancellationToken);
    }

    public async Task UpsertAsync(
        ChromaCollection collection,
        IReadOnlyCollection<ChromaRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
        {
            return;
        }

        foreach (var record in records)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(record.Id);
            ArgumentNullException.ThrowIfNull(record.Document);
            if (record.Embedding.Count == 0 || record.Embedding.Any(value => !float.IsFinite(value)))
            {
                throw new ArgumentException("ChromaDB records require a finite, non-empty embedding.", nameof(records));
            }
        }

        var body = new
        {
            ids = records.Select(record => record.Id),
            documents = records.Select(record => record.Document),
            embeddings = records.Select(record => record.Embedding),
            metadatas = records.Select(record => record.Metadata)
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{CollectionPath(collection.Id)}/upsert")
        {
            Content = JsonContent.Create(body)
        };
        using var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response, "upsert ChromaDB records");
    }

    public async Task<IReadOnlyList<ChromaQueryMatch>> QueryAsync(
        ChromaCollection collection,
        IReadOnlyList<float> embedding,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Count == 0 || embedding.Any(value => !float.IsFinite(value)))
        {
            throw new ArgumentException("A finite, non-empty embedding is required.", nameof(embedding));
        }

        if (maxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), "Maximum results must be greater than zero.");
        }

        var body = new
        {
            query_embeddings = new[] { embedding },
            n_results = maxResults,
            include = new[] { "documents", "metadatas", "distances" }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{CollectionPath(collection.Id)}/query")
        {
            Content = JsonContent.Create(body)
        };
        using var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response, "query ChromaDB records");

        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken)
            ?? throw new ChromaDependencyException("ChromaDB returned an empty query response.");
        return ReadMatches(document.RootElement);
    }

    private string CollectionsPath => $"api/v2/tenants/{Uri.EscapeDataString(_options.Tenant)}/databases/{Uri.EscapeDataString(_options.Database)}/collections";

    private string CollectionPath(string collectionId) => $"{CollectionsPath}/{Uri.EscapeDataString(collectionId)}";

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ChromaDependencyException("ChromaDB is unavailable.", innerException: exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ChromaDependencyException("ChromaDB request timed out.", innerException: exception);
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new ChromaDependencyException(
                $"Unable to {operation}. ChromaDB returned {(int)response.StatusCode} ({response.ReasonPhrase}).",
                response.StatusCode);
        }
    }

    private static async Task<ChromaCollection> ReadCollectionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken)
            ?? throw new ChromaDependencyException("ChromaDB returned an empty collection response.");
        var root = document.RootElement;
        var id = root.GetProperty("id").GetString();
        var name = root.GetProperty("name").GetString();

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
        {
            throw new ChromaDependencyException("ChromaDB returned an invalid collection response.");
        }

        return new ChromaCollection(id, name);
    }

    private static IReadOnlyList<ChromaQueryMatch> ReadMatches(JsonElement root)
    {
        var ids = GetFirstArray(root, "ids");
        var documents = GetFirstArray(root, "documents");
        var metadata = GetFirstArray(root, "metadatas");
        var distances = GetFirstArray(root, "distances");
        var matches = new List<ChromaQueryMatch>(ids.GetArrayLength());

        for (var index = 0; index < ids.GetArrayLength(); index++)
        {
            matches.Add(new ChromaQueryMatch(
                ids[index].GetString() ?? string.Empty,
                GetOptionalString(documents, index),
                GetMetadata(metadata, index),
                GetOptionalSingle(distances, index)));
        }

        return matches;
    }

    private static JsonElement GetFirstArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var values) || values.ValueKind != JsonValueKind.Array || values.GetArrayLength() == 0)
        {
            return default;
        }

        return values[0];
    }

    private static string? GetOptionalString(JsonElement values, int index) =>
        values.ValueKind == JsonValueKind.Array && index < values.GetArrayLength() && values[index].ValueKind != JsonValueKind.Null
            ? values[index].GetString()
            : null;

    private static float? GetOptionalSingle(JsonElement values, int index) =>
        values.ValueKind == JsonValueKind.Array && index < values.GetArrayLength() && values[index].ValueKind == JsonValueKind.Number
            ? values[index].GetSingle()
            : null;

    private static IReadOnlyDictionary<string, string>? GetMetadata(JsonElement values, int index)
    {
        if (values.ValueKind != JsonValueKind.Array || index >= values.GetArrayLength() || values[index].ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return values[index]
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.ToString(), StringComparer.Ordinal);
    }
}
