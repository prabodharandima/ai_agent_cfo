using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests;

public class MockChatClientTests
{
    [Theory]
    [InlineData("[MOCK:CLASSIFY] Give me the sales summary of this week.", "SalesSummary")]
    [InlineData("[MOCK:CLASSIFY] Compare this week versus last week.", "SalesComparison")]
    [InlineData("[MOCK:CLASSIFY] Show the top five products.", "TopProducts")]
    [InlineData("[MOCK:CLASSIFY] Give me a five year sales forecast.", "Forecast")]
    [InlineData("[MOCK:CLASSIFY] What annual target assumptions were used?", "Knowledge")]
    [InlineData("[MOCK:CLASSIFY] Give me a forecast and the assumptions used.", "Mixed")]
    [InlineData("[MOCK:CLASSIFY] Write a poem.", "Unsupported")]
    public async Task ClassifiesSupportedRequestsDeterministically(string request, string expectedIntent)
    {
        using var client = CreateClient();

        var first = await GetTextAsync(client, request);
        var second = await GetTextAsync(client, request);

        Assert.Equal(expectedIntent, first);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("[MOCK:SALES_SUMMARY] {\"netRevenue\":123.45,\"orders\":2}", "Mock sales executive summary based only on verified context:\n{\"netRevenue\":123.45,\"orders\":2}")]
    [InlineData("[MOCK:SALES_COMPARISON] {\"netRevenueChange\":23}", "Mock sales comparison based only on verified context:\n{\"netRevenueChange\":23}")]
    [InlineData("[MOCK:TOP_PRODUCTS] {\"productCode\":\"FIN-001\"}", "Mock top-products explanation based only on verified context:\n{\"productCode\":\"FIN-001\"}")]
    [InlineData("[MOCK:FORECAST] {\"year\":2027,\"expected\":456.78}", "Mock forecast explanation based only on verified context:\n{\"year\":2027,\"expected\":456.78}")]
    [InlineData("[MOCK:KNOWLEDGE] Annual target is 3000000.", "Mock knowledge answer based only on verified context:\nAnnual target is 3000000.")]
    public async Task FormatsOnlyTheSuppliedVerifiedContext(string request, string expected)
    {
        using var client = CreateClient();

        var response = await GetTextAsync(client, request);

        Assert.Equal(expected, response);
    }

    [Fact]
    public async Task ReturnsAStableUnsupportedResponseAndMockMetadata()
    {
        using var client = CreateClient();

        var response = await GetTextAsync(client, "Unmarked request");
        var metadata = Assert.IsType<ChatClientMetadata>(client.GetService(typeof(ChatClientMetadata)));

        Assert.Equal("Mock response: This request is outside the CFO MVP scope.", response);
        Assert.Equal("Mock", metadata.ProviderName);
        Assert.Equal("DeterministicMock", metadata.DefaultModelId);
    }

    [Fact]
    public async Task StreamsTheSameDeterministicResponse()
    {
        using var client = CreateClient();
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "[MOCK:CLASSIFY] Give me a forecast.")]))
        {
            updates.Add(update);
        }

        var streamedResponse = Assert.Single(updates);
        Assert.Equal("Forecast", streamedResponse.Text);
        Assert.Equal("DeterministicMock", streamedResponse.ModelId);
    }

    [Fact]
    public async Task HonorsConfiguredFailureSimulation()
    {
        using var client = CreateClient(simulateFailure: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => GetTextAsync(client, "[MOCK:CLASSIFY] sales"));

        Assert.Equal("Mock chat failure was requested by configuration.", exception.Message);
    }

    private static MockChatClient CreateClient(bool simulateFailure = false) => new(Options.Create(new AiOptions
    {
        Provider = "Mock",
        Model = "DeterministicMock",
        SimulateFailure = simulateFailure
    }));

    private static async Task<string> GetTextAsync(IChatClient client, string prompt)
    {
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
        return response.Text;
    }
}
