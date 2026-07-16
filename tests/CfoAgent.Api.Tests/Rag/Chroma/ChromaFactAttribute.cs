using Xunit;

namespace CfoAgent.Api.Tests.Rag.Chroma;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ChromaFactAttribute : FactAttribute
{
    public ChromaFactAttribute()
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri("http://localhost:8000/"), Timeout = TimeSpan.FromSeconds(2) };
            using var response = client.GetAsync("api/v2/heartbeat").GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                Skip = $"ChromaDB integration test skipped because Docker returned {(int)response.StatusCode}.";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Skip = $"ChromaDB integration test skipped because Docker is unavailable: {exception.Message}";
        }
    }
}
