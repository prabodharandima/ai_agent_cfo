using CfoAgent.Api.Caching;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Embeddings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Caching;

public sealed class CachedEmbeddingGeneratorTests
{
    [Fact]
    public async Task IdenticalTextReusesTheCachedVector()
    {
        var cache = new RecordingMemoryCache();
        var inner = new RecordingEmbeddingGenerator();
        var generator = CreateGenerator(inner, cache);

        var first = await generator.GenerateAsync(["annual sales target"]);
        var second = await generator.GenerateAsync(["  ANNUAL   SALES TARGET  "]);

        Assert.Equal(1, inner.Calls);
        Assert.Equal(Assert.Single(first).Vector.ToArray(), Assert.Single(second).Vector.ToArray());
        var key = cache.Requests.First().Key;
        Assert.All(cache.Requests, request => Assert.Equal(key, request.Key));
        Assert.All(cache.Requests, request => Assert.Equal(TimeSpan.FromSeconds(123), request.TimeToLive));
        Assert.StartsWith("embedding:v1:", key, StringComparison.Ordinal);
        Assert.DoesNotContain("annual sales target", key, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MixedHitAndMissBatchPreservesInputOrder()
    {
        var cache = new RecordingMemoryCache();
        var inner = new RecordingEmbeddingGenerator();
        var generator = CreateGenerator(inner, cache);

        await generator.GenerateAsync(["known"]);
        var result = await generator.GenerateAsync(["known", "new", "known"]);

        Assert.Equal(2, inner.Calls);
        Assert.Equal(
            [VectorFor("known"), VectorFor("new"), VectorFor("known")],
            result.Select(embedding => embedding.Vector.ToArray()).ToArray());
    }

    [Fact]
    public async Task DifferentTextOrVersionUsesDifferentCacheKeys()
    {
        var cache = new RecordingMemoryCache();
        var inner = new RecordingEmbeddingGenerator();
        var v1 = CreateGenerator(inner, cache, version: "v1");
        var v2 = CreateGenerator(inner, cache, version: "v2");

        await v1.GenerateAsync(["first"]);
        await v1.GenerateAsync(["second"]);
        await v2.GenerateAsync(["first"]);

        Assert.Equal(3, inner.Calls);
        Assert.Equal(3, cache.Requests.Select(request => request.Key).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task CacheFailureFallsBackToTheInnerGenerator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache, ThrowingDistributedCache>();
        services.AddHybridCache();
        using var serviceProvider = services.BuildServiceProvider();
        var cache = new HybridApplicationCache(
            serviceProvider.GetRequiredService<HybridCache>(),
            Options.Create(new CacheOptions { Enabled = true }),
            NullLogger<HybridApplicationCache>.Instance);
        var inner = new RecordingEmbeddingGenerator();
        var generator = CreateGenerator(inner, cache);

        var result = await generator.GenerateAsync(["annual target"]);

        Assert.Equal(1, inner.Calls);
        Assert.Equal(VectorFor("annual target"), Assert.Single(result).Vector.ToArray());
    }

    [Fact]
    public async Task CancellationIsPropagatedAndNotCached()
    {
        var cache = new RecordingMemoryCache();
        using var cancellationSource = new CancellationTokenSource();
        var inner = new RecordingEmbeddingGenerator
        {
            GenerateOverride = (_, _) =>
            {
                cancellationSource.Cancel();
                return Task.FromCanceled<GeneratedEmbeddings<Embedding<float>>>(cancellationSource.Token);
            }
        };
        var generator = CreateGenerator(inner, cache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => generator.GenerateAsync(["annual target"], cancellationToken: cancellationSource.Token));

        inner.GenerateOverride = null;
        await generator.GenerateAsync(["annual target"]);

        Assert.Equal(2, inner.Calls);
    }

    private static CachedEmbeddingGenerator CreateGenerator(
        IEmbeddingGenerator<string, Embedding<float>> inner,
        IApplicationCache cache,
        string version = "v1") =>
        new(
            inner,
            cache,
            Options.Create(new CacheOptions
            {
                Embeddings = new EmbeddingCacheOptions
                {
                    TtlSeconds = 123,
                    Version = version
                }
            }),
            "TestEmbeddingProvider",
            3);

    private static float[] VectorFor(string value) =>
        [value.Length, value.Count(character => character == 'a'), value.Count(character => character == 'e')];

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

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            values.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public int Calls { get; private set; }

        public Func<string[], CancellationToken, Task<GeneratedEmbeddings<Embedding<float>>>>? GenerateOverride { get; set; }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            var requested = values.ToArray();
            if (GenerateOverride is not null)
            {
                return GenerateOverride(requested, cancellationToken);
            }

            var result = new GeneratedEmbeddings<Embedding<float>>();
            foreach (var value in requested)
            {
                result.Add(new Embedding<float>(VectorFor(value)));
            }

            return Task.FromResult(result);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
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

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            throw new InvalidOperationException("Cache unavailable.");
    }

    private sealed record CacheRequest(string Key, TimeSpan TimeToLive);
}
