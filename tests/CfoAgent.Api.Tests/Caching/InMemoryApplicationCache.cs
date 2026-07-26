using CfoAgent.Api.Caching;

namespace CfoAgent.Api.Tests.Caching;

internal sealed class InMemoryApplicationCache : IApplicationCache
{
    private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);

    public List<CacheRequest> Requests { get; } = [];

    public List<string> RemovedKeys { get; } = [];

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan timeToLive,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
        RemovedKeys.Add(key);
        values.Remove(key);
        return Task.CompletedTask;
    }

    internal sealed record CacheRequest(string Key, TimeSpan TimeToLive);
}
