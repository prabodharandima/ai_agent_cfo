using CfoAgent.Api.AI.Mock;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests;

public sealed class MockModelMetadataTests
{
    [Fact]
    public void MockClientReportsTheConfiguredDeterministicModel()
    {
        using var client = new MockChatClient(Options.Create(new AiOptions
        {
            Provider = "Mock",
            Model = "DeterministicMock"
        }));

        Assert.Equal("Mock", client.Metadata.ProviderName);
        Assert.Equal("DeterministicMock", client.Metadata.DefaultModelId);
    }
}
