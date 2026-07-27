namespace CfoAgent.Api.Caching;

public interface IApplicationCache
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan timeToLive,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken);

    Task RemoveAsync(string key, CancellationToken cancellationToken);
}
