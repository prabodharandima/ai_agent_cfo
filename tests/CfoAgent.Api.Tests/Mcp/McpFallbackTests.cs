using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class McpFallbackTests
{
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
            _ => throw new McpDependencyException("Knowledge File MCP", McpDependencyFailureKind.Unavailable),
            _ => Task.FromResult("direct"),
            CancellationToken.None);

        Assert.Equal("direct", result.Value);
        Assert.True(result.UsedFallback);
        Assert.Equal("unavailable", result.FallbackReason);
    }

    [Fact]
    public async Task KnowledgeFallbackMustBeExplicitlyEnabled()
    {
        var policy = CreateKnowledgeFallback(knowledgeEnabled: false, useLocalFallback: false);
        var directCalled = false;

        var exception = await Assert.ThrowsAsync<McpDependencyException>(() => policy.ExecuteAsync(
            _ => Task.FromResult("mcp"),
            _ => { directCalled = true; return Task.FromResult("direct"); },
            CancellationToken.None));

        Assert.Equal(McpDependencyFailureKind.Disabled, exception.FailureKind);
        Assert.False(directCalled);
    }

    [Fact]
    public async Task KnowledgeFallbackIsRejectedOutsideDevelopment()
    {
        var policy = CreateKnowledgeFallback(knowledgeEnabled: true, useLocalFallback: true, Environments.Production);
        var directCalled = false;

        await Assert.ThrowsAsync<McpDependencyException>(() => policy.ExecuteAsync<string>(
            _ => throw new McpDependencyException("Knowledge File MCP", McpDependencyFailureKind.Unavailable),
            _ => { directCalled = true; return Task.FromResult("direct"); },
            CancellationToken.None));

        Assert.False(directCalled);
    }

    private static KnowledgeFileMcpFallback CreateKnowledgeFallback(
        bool knowledgeEnabled,
        bool useLocalFallback = true,
        string environmentName = "Development") => new(
        Options.Create(CreateOptions(financeEnabled: false, knowledgeEnabled, useLocalFallback)),
        new TestHostEnvironment { EnvironmentName = environmentName },
        NullLogger<KnowledgeFileMcpFallback>.Instance);

    private static McpOptions CreateOptions(bool financeEnabled, bool knowledgeEnabled, bool useLocalFallback = true) => new()
    {
        Finance = new FinanceMcpOptions
        {
            Enabled = financeEnabled,
            BaseUrl = "http://finance-mcp.test",
            TimeoutSeconds = 1
        },
        KnowledgeFiles = new KnowledgeFileMcpOptions
        {
            Enabled = knowledgeEnabled,
            BaseUrl = "http://knowledge-mcp.test",
            RootPath = "unused",
            UseLocalFallback = useLocalFallback,
            TimeoutSeconds = 1
        }
    };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "CfoAgent.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
