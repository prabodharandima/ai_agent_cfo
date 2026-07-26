using CfoAgent.Api.AI;
using CfoAgent.Api.Agents;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Caching;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Tests.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Caching;

public sealed class CachedIntentClassificationTests
{
    [Fact]
    public async Task IdenticalStatelessPrompt_ReusesValidatedClassification()
    {
        var calls = 0;
        using var client = CreateClient(() => calls++);
        var cache = new InMemoryApplicationCache();
        var orchestrator = CreateOrchestrator(client, cache);

        var first = await orchestrator.ClassifyAsync(new AgentRequest("Give me the sales summary of this week."));
        var second = await orchestrator.ClassifyAsync(new AgentRequest("  Give me the sales summary of this week.  "));

        Assert.Equal(CfoIntent.SalesSummary, first);
        Assert.Equal(first, second);
        Assert.Equal(1, calls);
        Assert.Equal(2, cache.Requests.Count);
        Assert.Single(cache.Requests.Select(request => request.Key).Distinct(StringComparer.Ordinal));
        Assert.All(cache.Requests, request => Assert.Equal(TimeSpan.FromSeconds(300), request.TimeToLive));
    }

    [Fact]
    public async Task ProviderModelAndPromptVersionChanges_UseDifferentCacheKeys()
    {
        var cache = new InMemoryApplicationCache();
        using var client = CreateClient();

        await CreateOrchestrator(client, cache, provider: new AiProviderDescriptor("Ollama", "model-a"))
            .ClassifyAsync(new AgentRequest("Give me the sales summary of this week."));
        await CreateOrchestrator(client, cache, provider: new AiProviderDescriptor("Alternate", "model-a"))
            .ClassifyAsync(new AgentRequest("Give me the sales summary of this week."));
        await CreateOrchestrator(client, cache, provider: new AiProviderDescriptor("Ollama", "model-b"))
            .ClassifyAsync(new AgentRequest("Give me the sales summary of this week."));
        await CreateOrchestrator(
                client,
                cache,
                promptVersion: "v2",
                provider: new AiProviderDescriptor("Ollama", "model-a"))
            .ClassifyAsync(new AgentRequest("Give me the sales summary of this week."));

        Assert.Equal(4, cache.Requests.Select(request => request.Key).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("{not-json}")]
    [InlineData("{\"intent\":\"Unsupported\"}")]
    public async Task MalformedOrFallbackClassification_IsNotCached(string response)
    {
        var calls = 0;
        using var client = new TestChatClient((_, _, _) =>
        {
            calls++;
            return Task.FromResult(response);
        });
        var cache = new InMemoryApplicationCache();
        var orchestrator = CreateOrchestrator(client, cache);
        var request = new AgentRequest("Show the top products this month.");

        var first = await orchestrator.ClassifyAsync(request);
        var second = await orchestrator.ClassifyAsync(request);

        Assert.Equal(CfoIntent.TopProducts, first);
        Assert.Equal(first, second);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ProviderFailure_IsNotCached()
    {
        var calls = 0;
        using var client = new TestChatClient((_, _, _) =>
        {
            calls++;
            return Task.FromException<string>(new AiProviderException("Ollama", AiProviderFailureKind.Unavailable));
        });
        var cache = new InMemoryApplicationCache();
        var orchestrator = CreateOrchestrator(client, cache);
        var request = new AgentRequest("Give me the sales summary of this week.");

        await Assert.ThrowsAsync<AiProviderException>(() => orchestrator.ClassifyAsync(request));
        await Assert.ThrowsAsync<AiProviderException>(() => orchestrator.ClassifyAsync(request));

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task CacheFailure_FallsBackToNormalClassification()
    {
        var calls = 0;
        using var client = CreateClient(() => calls++);
        using var serviceProvider = CreateFailingHybridCacheServiceProvider();
        var cache = new HybridApplicationCache(
            serviceProvider.GetRequiredService<HybridCache>(),
            Options.Create(new CacheOptions { Enabled = true }),
            NullLogger<HybridApplicationCache>.Instance);
        var orchestrator = CreateOrchestrator(client, cache);

        var intent = await orchestrator.ClassifyAsync(new AgentRequest("Give me the sales summary of this week."));

        Assert.Equal(CfoIntent.SalesSummary, intent);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CallerCancellation_IsNotCached()
    {
        var calls = 0;
        using var client = new TestChatClient(async (_, _, cancellationToken) =>
        {
            calls++;
            if (calls == 1)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return "{\"intent\":\"SalesSummary\"}";
        });
        var cache = new InMemoryApplicationCache();
        var orchestrator = CreateOrchestrator(client, cache);
        var request = new AgentRequest("Give me the sales summary of this week.");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => orchestrator.ClassifyAsync(request, cancellation.Token));
        var intent = await orchestrator.ClassifyAsync(request);

        Assert.Equal(CfoIntent.SalesSummary, intent);
        Assert.Equal(2, calls);
        Assert.Equal(2, cache.Requests.Count);
    }

    [Fact]
    public async Task SessionDependentClassification_UsesSafeContextFingerprint()
    {
        var calls = 0;
        using var client = CreateClient(() => calls++);
        var cache = new InMemoryApplicationCache();
        var orchestrator = CreateOrchestrator(client, cache);
        const string message = "Give me the sales summary of this week.";
        var salesContext = new AgentSessionContext([
            new AgentSessionTurn(
                AgentResponseType.SalesSummary,
                new AgentDataPeriod(new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 19), "Current week"))
        ]);
        var relabeledSalesContext = new AgentSessionContext([
            new AgentSessionTurn(
                AgentResponseType.SalesSummary,
                new AgentDataPeriod(new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 19), "Requested period"))
        ]);

        await orchestrator.ClassifyAsync(new AgentRequest(message, salesContext));
        await orchestrator.ClassifyAsync(new AgentRequest(message, relabeledSalesContext));
        await orchestrator.ClassifyAsync(new AgentRequest(message, salesContext));

        Assert.Equal(2, calls);
        Assert.Equal(2, cache.Requests.Select(request => request.Key).Distinct(StringComparer.Ordinal).Count());
    }

    private static TestChatClient CreateClient(Action? onCall = null) => new((_, _, _) =>
    {
        onCall?.Invoke();
        return Task.FromResult("{\"intent\":\"SalesSummary\"}");
    });

    private static CfoOrchestratorAgent CreateOrchestrator(
        TestChatClient client,
        IApplicationCache cache,
        string promptVersion = "v1",
        AiProviderDescriptor? provider = null) => new(
            null!,
            null!,
            null!,
            new AgentResultComposer(),
            client,
            applicationCache: cache,
            cacheOptions: Options.Create(new CacheOptions
            {
                Enabled = true,
                Classification = new LlmClassificationCacheOptions
                {
                    TtlSeconds = 300,
                    PromptVersion = promptVersion,
                    AllowedIntentSetVersion = "v1"
                }
            }),
            aiProvider: provider ?? new AiProviderDescriptor("Test", "test-model"),
            agentMiddlewareOptions: Options.Create(new AgentMiddlewareOptions
            {
                PromptInjectionCheckEnabled = true,
                SuspiciousPromptPhrases = ["ignore previous instructions"]
            }));

    private static ServiceProvider CreateFailingHybridCacheServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache, ThrowingDistributedCache>();
        services.AddHybridCache();
        return services.BuildServiceProvider();
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
}
