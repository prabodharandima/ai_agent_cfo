using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.AI;

public class MockChatClientGateTests
{
    [Theory]
    [InlineData("[MOCK:CLASSIFY] Give me the sales summary of this week.", "SalesSummary")]
    [InlineData("[MOCK:CLASSIFY] Compare this week to last week.", "SalesComparison")]
    [InlineData("[MOCK:CLASSIFY] Show the top products this month.", "TopProducts")]
    [InlineData("[MOCK:CLASSIFY] Give me the sales forecast for the next five years.", "Forecast")]
    public async Task CurrentPhaseIntentsHaveStableOfflineResponses(string prompt, string expectedIntent)
    {
        using var client = CreateClient();

        var first = await GetTextAsync(client, prompt, CancellationToken.None);
        var second = await GetTextAsync(client, prompt, CancellationToken.None);

        Assert.Equal(expectedIntent, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task CancellationStopsTheConfiguredMockDelay()
    {
        using var client = CreateClient(simulatedDelayMilliseconds: 5_000);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            GetTextAsync(client, "[MOCK:CLASSIFY] sales", cancellationSource.Token));
    }

    private static MockChatClient CreateClient(int simulatedDelayMilliseconds = 0) => new(Options.Create(new AiOptions
    {
        Provider = "Mock",
        Model = "DeterministicMock",
        SimulatedDelayMilliseconds = simulatedDelayMilliseconds
    }));

    private static async Task<string> GetTextAsync(IChatClient client, string prompt, CancellationToken cancellationToken)
    {
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)], cancellationToken: cancellationToken);
        return response.Text;
    }
}
