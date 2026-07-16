using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class McpFallbackTests
{
    [Fact]
    public async Task FinanceDisabledUsesLocalFallback()
    {
        var policy = CreateFinanceFallback(financeEnabled: false);
        var mcpCalled = false;

        var result = await policy.ExecuteAsync(
            _ => { mcpCalled = true; return Task.FromResult("mcp"); },
            _ => Task.FromResult("local"),
            CancellationToken.None);

        Assert.Equal("local", result.Value);
        Assert.True(result.UsedFallback);
        Assert.Equal("disabled", result.FallbackReason);
        Assert.False(mcpCalled);
    }

    [Fact]
    public async Task FinanceInitializationFailureUsesLocalFallback()
    {
        var policy = CreateFinanceFallback(financeEnabled: true);

        var result = await policy.ExecuteAsync<string>(
            _ => throw new InvalidOperationException("Initialization failed."),
            _ => Task.FromResult("local"),
            CancellationToken.None);

        Assert.Equal("local", result.Value);
        Assert.True(result.UsedFallback);
        Assert.Equal("unavailable", result.FallbackReason);
    }

    [Fact]
    public async Task FinanceTimeoutUsesLocalFallback()
    {
        var policy = CreateFinanceFallback(financeEnabled: true);

        var result = await policy.ExecuteAsync<string>(
            _ => throw new OperationCanceledException("MCP request timed out."),
            _ => Task.FromResult("local"),
            CancellationToken.None);

        Assert.Equal("local", result.Value);
        Assert.True(result.UsedFallback);
        Assert.Equal("timeout", result.FallbackReason);
    }

    [Fact]
    public async Task KnowledgeFilesDisabledUsesDirectFallback()
    {
        var policy = CreateKnowledgeFallback(knowledgeEnabled: false);
        var mcpCalled = false;

        var result = await policy.ExecuteAsync(
            _ => { mcpCalled = true; return Task.FromResult("mcp"); },
            _ => Task.FromResult("direct"),
            CancellationToken.None);

        Assert.Equal("direct", result.Value);
        Assert.True(result.UsedFallback);
        Assert.Equal("disabled", result.FallbackReason);
        Assert.False(mcpCalled);
    }

    [Fact]
    public async Task KnowledgeFileFailureUsesDirectFallback()
    {
        var policy = CreateKnowledgeFallback(knowledgeEnabled: true);

        var result = await policy.ExecuteAsync<string>(
            _ => throw new IOException("Knowledge MCP unavailable."),
            _ => Task.FromResult("direct"),
            CancellationToken.None);

        Assert.Equal("direct", result.Value);
        Assert.True(result.UsedFallback);
        Assert.Equal("unavailable", result.FallbackReason);
    }

    private static FinanceMcpFallback CreateFinanceFallback(bool financeEnabled) => new(
        Options.Create(CreateOptions(financeEnabled, knowledgeEnabled: false)),
        NullLogger<FinanceMcpFallback>.Instance);

    private static KnowledgeFileMcpFallback CreateKnowledgeFallback(bool knowledgeEnabled) => new(
        Options.Create(CreateOptions(financeEnabled: false, knowledgeEnabled)),
        NullLogger<KnowledgeFileMcpFallback>.Instance);

    private static McpOptions CreateOptions(bool financeEnabled, bool knowledgeEnabled) => new()
    {
        UseLocalFallback = true,
        Finance = new FinanceMcpOptions
        {
            Enabled = financeEnabled,
            ServerProjectPath = "unused",
            TimeoutSeconds = 1
        },
        KnowledgeFiles = new KnowledgeFileMcpOptions
        {
            Enabled = knowledgeEnabled,
            RootPath = "unused",
            TimeoutSeconds = 1
        }
    };
}
