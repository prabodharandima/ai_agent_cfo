using CfoAgent.Api.Caching;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Caching;

public sealed class CachedFinancialKnowledgeSearchTests
{
    [Fact]
    public async Task IdenticalNormalizedQueryCallsInnerSearchOnce()
    {
        var cache = new RecordingMemoryCache();
        var inner = new RecordingKnowledgeSearch();
        var search = CreateSearch(inner, cache);

        await search.RetrieveAsync(new FinancialKnowledgeQuery(" What is the annual target? "));
        await search.RetrieveAsync(new FinancialKnowledgeQuery("what   is the annual target?"));

        Assert.Equal(1, inner.Calls);
        var key = cache.Requests.First().Key;
        Assert.All(cache.Requests, request => Assert.Equal(key, request.Key));
        Assert.DoesNotContain("What is the annual target", key, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("rag:v1:retrieval:v1:", key, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangedFiltersTopKOrThresholdUseDifferentKeys()
    {
        var cache = new RecordingMemoryCache();
        var inner = new RecordingKnowledgeSearch();
        var defaultSearch = CreateSearch(inner, cache);
        var stricterSearch = CreateSearch(
            inner,
            cache,
            ragOptions: new RagOptions
            {
                KnowledgeFilesRoot = "unused",
                MaxChunkCharacters = 256,
                MaxKnowledgeContextCharacters = 4000,
                MaximumRetrievalDistance = 0.5f,
                IndexVersion = "v1"
            });

        await defaultSearch.RetrieveAsync(new FinancialKnowledgeQuery("What is the annual target?", TopK: 3));
        await defaultSearch.RetrieveAsync(new FinancialKnowledgeQuery(
            "What is the annual target?",
            TopK: 4,
            DocumentType: "budget_target",
            Period: "2026"));
        await stricterSearch.RetrieveAsync(new FinancialKnowledgeQuery("What is the annual target?", TopK: 3));

        Assert.Equal(3, inner.Calls);
        Assert.Equal(3, cache.Requests.Select(request => request.Key).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task ChangedIndexVersionBypassesPreviousEntry()
    {
        var cache = new RecordingMemoryCache();
        var inner = new RecordingKnowledgeSearch();
        var v1 = CreateSearch(inner, cache, ragOptions: CreateRagOptions(indexVersion: "v1"));
        var v2 = CreateSearch(inner, cache, ragOptions: CreateRagOptions(indexVersion: "v2"));
        var query = new FinancialKnowledgeQuery("What is the annual target?");

        await v1.RetrieveAsync(query);
        await v2.RetrieveAsync(query);

        Assert.Equal(2, inner.Calls);
        Assert.Contains(cache.Requests, request => request.Key.StartsWith("rag:v1:retrieval:v1:", StringComparison.Ordinal));
        Assert.Contains(cache.Requests, request => request.Key.StartsWith("rag:v1:retrieval:v2:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CacheFailureFallsBackToChromaSearch()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache, ThrowingDistributedCache>();
        services.AddHybridCache();
        using var serviceProvider = services.BuildServiceProvider();
        var cache = new HybridApplicationCache(
            serviceProvider.GetRequiredService<HybridCache>(),
            Options.Create(new CacheOptions { Enabled = true }),
            NullLogger<HybridApplicationCache>.Instance);
        var inner = new RecordingKnowledgeSearch();
        var search = CreateSearch(inner, cache);

        var result = await search.RetrieveAsync(new FinancialKnowledgeQuery("What is the annual target?"));

        Assert.True(result.HasSufficientKnowledge);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task CancellationIsPropagatedAndNotCached()
    {
        using var services = CreateHybridCacheServices();
        var cache = new HybridApplicationCache(
            services.GetRequiredService<HybridCache>(),
            Options.Create(new CacheOptions { Enabled = true }),
            NullLogger<HybridApplicationCache>.Instance);
        using var cancellationSource = new CancellationTokenSource();
        var inner = new RecordingKnowledgeSearch
        {
            RetrieveOverride = _ =>
            {
                cancellationSource.Cancel();
                return Task.FromCanceled<FinancialKnowledgeRetrievalResult>(cancellationSource.Token);
            }
        };
        var search = CreateSearch(inner, cache);
        var query = new FinancialKnowledgeQuery("What is the annual target?");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search.RetrieveAsync(query, cancellationSource.Token));

        inner.RetrieveOverride = _ => Task.FromResult(CreateResult());
        var result = await search.RetrieveAsync(query);

        Assert.True(result.HasSufficientKnowledge);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task CachedResultPreservesSourcesWarningsAndConfiguredTtl()
    {
        var cache = new RecordingMemoryCache();
        var inner = new RecordingKnowledgeSearch();
        var search = CreateSearch(
            inner,
            cache,
            cacheOptions: new CacheOptions { Rag = new RagCacheOptions { RetrievalTtlSeconds = 123 } });
        var query = new FinancialKnowledgeQuery("What is the annual target?");

        var first = await search.RetrieveAsync(query);
        var second = await search.RetrieveAsync(query);

        Assert.Equal(1, inner.Calls);
        Assert.Equal(first.Sources, second.Sources);
        Assert.Equal(first.Warnings, second.Warnings);
        Assert.All(cache.Requests, request => Assert.Equal(TimeSpan.FromSeconds(123), request.TimeToLive));
        var source = Assert.Single(second.Sources);
        Assert.Equal("data/knowledge/current-budget-and-target.md", source.SourcePath);
        Assert.Equal("Annual Target", source.Section);
    }

    private static CachedFinancialKnowledgeSearch CreateSearch(
        IFinancialKnowledgeSearch inner,
        IApplicationCache cache,
        CacheOptions? cacheOptions = null,
        RagOptions? ragOptions = null) =>
        new(
            inner,
            cache,
            Options.Create(cacheOptions ?? new CacheOptions()),
            Options.Create(ragOptions ?? CreateRagOptions()));

    private static RagOptions CreateRagOptions(string indexVersion = "v1") => new()
    {
        KnowledgeFilesRoot = "unused",
        MaxChunkCharacters = 256,
        MaxKnowledgeContextCharacters = 4000,
        MaximumRetrievalDistance = 1.25f,
        IndexVersion = indexVersion
    };

    private static ServiceProvider CreateHybridCacheServices()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }

    private static FinancialKnowledgeRetrievalResult CreateResult() => new(
        [new FinancialKnowledgeSource(
            "target-1",
            "current-budget-target-2026",
            "Current Budget And Annual Target",
            "budget_target",
            "2026",
            "Annual Target",
            "data/knowledge/current-budget-and-target.md",
            "The FY2026 sales target is 3000000.",
            0.1f)],
        ["Retrieved from the indexed budget document."]);

    private sealed class RecordingMemoryCache : IApplicationCache
    {
        private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);

        public List<CacheRequest> Requests { get; } = [];

        public async Task<T> GetOrCreateAsync<T>(
            string key,
            TimeSpan timeToLive,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken)
        {
            Requests.Add(new CacheRequest(key, timeToLive));
            if (values.TryGetValue(key, out var cached))
            {
                return (T)cached!;
            }

            var value = await factory(cancellationToken);
            values[key] = value;
            return value;
        }
    }

    private sealed class RecordingKnowledgeSearch : IFinancialKnowledgeSearch
    {
        public int Calls { get; private set; }

        public Func<CancellationToken, Task<FinancialKnowledgeRetrievalResult>>? RetrieveOverride { get; set; }

        public Task<FinancialKnowledgeRetrievalResult> RetrieveAsync(
            FinancialKnowledgeQuery query,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return RetrieveOverride?.Invoke(cancellationToken) ?? Task.FromResult(CreateResult());
        }
    }

    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("Cache unavailable.");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Cache unavailable.");

        public void Refresh(string key) => throw new InvalidOperationException("Cache unavailable.");

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Cache unavailable.");

        public void Remove(string key) => throw new InvalidOperationException("Cache unavailable.");

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Cache unavailable.");

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new InvalidOperationException("Cache unavailable.");

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default) =>
            throw new InvalidOperationException("Cache unavailable.");
    }

    private sealed record CacheRequest(string Key, TimeSpan TimeToLive);
}
