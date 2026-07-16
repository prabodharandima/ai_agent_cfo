using System.Net;
using CfoAgent.Api.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CfoAgent.Api.Tests;

public class ChromaHealthCheckTests
{
    [Fact]
    public async Task ReturnsHealthyWhenChromaRespondsSuccessfully()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(HttpStatusCode.OK))
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        var healthCheck = new ChromaHealthCheck(httpClient);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task ReturnsControlledUnhealthyResultWhenChromaIsUnavailable()
    {
        using var httpClient = new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        var healthCheck = new ChromaHealthCheck(httpClient);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("ChromaDB is unavailable.", result.Description);
    }

    private sealed class StaticResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException();
        }
    }
}
