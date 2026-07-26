using CfoAgent.Api.Caching;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Tests.Finance;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Caching;

public sealed class CachedFinanceMcpClientTests
{
    private static readonly DateOnly CurrentDate = new(2026, 7, 15);
    private static readonly SalesPeriod CurrentPeriod = new(new DateOnly(2026, 7, 13), CurrentDate);

    [Fact]
    public async Task CacheMissCallsInnerClientOnce()
    {
        var cache = new RecordingMemoryCache();
        var inner = new RecordingFinanceMcpClient();
        var client = CreateClient(inner, cache);

        var result = await client.GetSalesSummaryAsync(CurrentPeriod, CancellationToken.None);

        Assert.Equal(CurrentPeriod, result.Period);
        Assert.Equal(1, inner.SalesSummaryCalls);
        Assert.Equal("finance:v1:sales-summary:20260713:20260715", Assert.Single(cache.Requests).Key);
    }

    [Fact]
    public async Task RepeatedIdenticalRequestUsesCachedResult()
    {
        var cache = new RecordingMemoryCache();
        var inner = new RecordingFinanceMcpClient();
        var client = CreateClient(inner, cache);

        var first = await client.GetSalesSummaryAsync(CurrentPeriod, CancellationToken.None);
        var second = await client.GetSalesSummaryAsync(CurrentPeriod, CancellationToken.None);

        Assert.Same(first, second);
        Assert.Equal(1, inner.SalesSummaryCalls);
    }

    [Fact]
    public async Task DifferentArgumentsUseDifferentKeys()
    {
        var cache = new RecordingMemoryCache();
        var inner = new RecordingFinanceMcpClient();
        var client = CreateClient(inner, cache);

        await client.GetSalesSummaryAsync(CurrentPeriod, CancellationToken.None);
        await client.GetSalesSummaryAsync(
            new SalesPeriod(new DateOnly(2026, 7, 1), CurrentDate),
            CancellationToken.None);
        await client.GetBudgetTargetAsync(2026, null, CancellationToken.None);
        await client.GetBudgetTargetAsync(2026, 7, CancellationToken.None);

        Assert.Equal(2, inner.SalesSummaryCalls);
        Assert.Equal(2, inner.BudgetTargetCalls);
        Assert.Equal(
            [
                "finance:v1:sales-summary:20260713:20260715",
                "finance:v1:sales-summary:20260701:20260715",
                "finance:v1:budget-target:2026:annual",
                "finance:v1:budget-target:2026:07"
            ],
            cache.Requests.Select(request => request.Key));
    }

    [Fact]
    public async Task DisabledCacheAlwaysCallsInnerClient()
    {
        using var services = CreateHybridCacheServices();
        var cache = new HybridApplicationCache(
            services.GetRequiredService<HybridCache>(),
            Options.Create(new CacheOptions { Enabled = false }),
            NullLogger<HybridApplicationCache>.Instance);
        var inner = new RecordingFinanceMcpClient();
        var client = CreateClient(inner, cache);

        await client.GetSalesSummaryAsync(CurrentPeriod, CancellationToken.None);
        await client.GetSalesSummaryAsync(CurrentPeriod, CancellationToken.None);

        Assert.Equal(2, inner.SalesSummaryCalls);
    }

    [Fact]
    public async Task CacheFailureFallsBackToAuthoritativeClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDistributedCache, ThrowingDistributedCache>();
        services.AddHybridCache();
        using var serviceProvider = services.BuildServiceProvider();
        var cache = new HybridApplicationCache(
            serviceProvider.GetRequiredService<HybridCache>(),
            Options.Create(new CacheOptions { Enabled = true }),
            NullLogger<HybridApplicationCache>.Instance);
        var inner = new RecordingFinanceMcpClient();
        var client = CreateClient(inner, cache);

        var result = await client.GetSalesSummaryAsync(CurrentPeriod, CancellationToken.None);

        Assert.Equal(CurrentPeriod, result.Period);
        Assert.Equal(1, inner.SalesSummaryCalls);
    }

    [Fact]
    public async Task CancellationIsPropagatedAndNotCached()
    {
        using var services = CreateHybridCacheServices();
        var cache = new HybridApplicationCache(
            services.GetRequiredService<HybridCache>(),
            Options.Create(new CacheOptions { Enabled = true }),
            NullLogger<HybridApplicationCache>.Instance);
        var cancellationSource = new CancellationTokenSource();
        var inner = new RecordingFinanceMcpClient
        {
            SalesSummaryOverride = _ =>
            {
                cancellationSource.Cancel();
                return Task.FromCanceled<SalesSummary>(cancellationSource.Token);
            }
        };
        var client = CreateClient(inner, cache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetSalesSummaryAsync(CurrentPeriod, cancellationSource.Token));

        inner.SalesSummaryOverride = periodToken => Task.FromResult(CreateSummary(CurrentPeriod));
        var result = await client.GetSalesSummaryAsync(CurrentPeriod, CancellationToken.None);

        Assert.Equal(CurrentPeriod, result.Period);
        Assert.Equal(2, inner.SalesSummaryCalls);
    }

    [Fact]
    public async Task FactoryFailureIsNotCached()
    {
        using var services = CreateHybridCacheServices();
        var cache = new HybridApplicationCache(
            services.GetRequiredService<HybridCache>(),
            Options.Create(new CacheOptions { Enabled = true }),
            NullLogger<HybridApplicationCache>.Instance);
        var inner = new RecordingFinanceMcpClient
        {
            SalesSummaryOverride = _ => throw new InvalidOperationException("Authoritative dependency failed.")
        };
        var client = CreateClient(inner, cache);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetSalesSummaryAsync(CurrentPeriod, CancellationToken.None));

        inner.SalesSummaryOverride = _ => Task.FromResult(CreateSummary(CurrentPeriod));
        await client.GetSalesSummaryAsync(CurrentPeriod, CancellationToken.None);

        Assert.Equal(2, inner.SalesSummaryCalls);
    }

    [Fact]
    public async Task AllFinanceOperationsUseTheirConfiguredTtl()
    {
        var cache = new RecordingMemoryCache();
        var inner = new RecordingFinanceMcpClient();
        var options = new CacheOptions
        {
            Finance = new FinanceCacheOptions
            {
                SalesSummaryTtlSeconds = 11,
                CurrentWeekSummaryTtlSeconds = 12,
                WeekOverWeekComparisonTtlSeconds = 13,
                CurrentMonthTopProductsTtlSeconds = 14,
                HistoricalYearlyTotalsTtlSeconds = 15,
                BudgetTargetTtlSeconds = 16
            }
        };
        var client = CreateClient(inner, cache, options);

        await client.GetSalesSummaryAsync(CurrentPeriod, CancellationToken.None);
        await client.GetCurrentWeekSummaryAsync(CancellationToken.None);
        await client.GetWeekOverWeekComparisonAsync(CancellationToken.None);
        await client.GetCurrentMonthTopProductsAsync(CancellationToken.None);
        await client.GetHistoricalYearlyTotalsAsync(CancellationToken.None);
        await client.GetBudgetTargetAsync(2026, 7, CancellationToken.None);

        Assert.Equal(
            [11, 12, 13, 14, 15, 16],
            cache.Requests.Select(request => (int)request.TimeToLive.TotalSeconds));
    }

    private static CachedFinanceMcpClient CreateClient(
        IFinanceMcpClient inner,
        IApplicationCache cache,
        CacheOptions? options = null) =>
        new(
            inner,
            cache,
            Options.Create(options ?? new CacheOptions()),
            new FixedTimeProvider(CurrentDate));

    private static ServiceProvider CreateHybridCacheServices()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }

    private static SalesSummary CreateSummary(SalesPeriod period) =>
        new(period, 100m, 40m, 60m, 60m, 1m, 1, 100m, null, Array.Empty<string>());

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

    private sealed class RecordingFinanceMcpClient : IFinanceMcpClient
    {
        public int SalesSummaryCalls { get; private set; }

        public int BudgetTargetCalls { get; private set; }

        public Func<CancellationToken, Task<SalesSummary>>? SalesSummaryOverride { get; set; }

        public Task<SalesSummary> GetSalesSummaryAsync(SalesPeriod period, CancellationToken cancellationToken)
        {
            SalesSummaryCalls++;
            return SalesSummaryOverride?.Invoke(cancellationToken) ?? Task.FromResult(CreateSummary(period));
        }

        public Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CreateSummary(CurrentPeriod));

        public Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new WeeklySalesComparison(
                CreateSummary(CurrentPeriod),
                CreateSummary(new SalesPeriod(new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 12))),
                0m,
                0m,
                SalesChangeDirection.Unchanged,
                Array.Empty<string>()));

        public Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new TopProductsResult(CurrentPeriod, Array.Empty<TopProduct>(), Array.Empty<string>()));

        public Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new HistoricalYearlySalesResult([new YearlySalesTotal(2025, 100m)], Array.Empty<string>()));

        public Task<BudgetTargetResult> GetBudgetTargetAsync(
            int year,
            int? month,
            CancellationToken cancellationToken)
        {
            BudgetTargetCalls++;
            return Task.FromResult(new BudgetTargetResult(
                year,
                month,
                true,
                100m,
                20m,
                "test",
                Array.Empty<string>()));
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
