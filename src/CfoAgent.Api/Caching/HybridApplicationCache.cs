using System.Runtime.ExceptionServices;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Caching;

public sealed class HybridApplicationCache(
    HybridCache hybridCache,
    IOptions<CacheOptions> options,
    ILogger<HybridApplicationCache> logger) : IApplicationCache
{
    private readonly CacheOptions options = options.Value;

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan timeToLive,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        if (timeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!options.Enabled)
        {
            return await factory(cancellationToken);
        }

        var invocation = new FactoryInvocation<T>();
        try
        {
            return await hybridCache.GetOrCreateAsync(
                key,
                async token =>
                {
                    try
                    {
                        var value = await factory(token);
                        invocation.SetValue(value);
                        return value;
                    }
                    catch (Exception exception)
                    {
                        invocation.SetException(exception);
                        throw;
                    }
                },
                new HybridCacheEntryOptions
                {
                    Expiration = timeToLive,
                    LocalCacheExpiration = timeToLive
                },
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch when (invocation.Exception is not null)
        {
            ExceptionDispatchInfo.Capture(invocation.Exception).Throw();
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Application cache unavailable; using the authoritative dependency. FailureType: {FailureType}.",
                exception.GetType().Name);

            return invocation.HasValue
                ? invocation.Value
                : await factory(cancellationToken);
        }
    }

    private sealed class FactoryInvocation<T>
    {
        public bool HasValue { get; private set; }

        public T Value { get; private set; } = default!;

        public Exception? Exception { get; private set; }

        public void SetValue(T value)
        {
            Value = value;
            HasValue = true;
        }

        public void SetException(Exception exception) => Exception = exception;
    }
}
