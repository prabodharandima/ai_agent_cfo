using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Mcp;

public sealed class KnowledgeFileMcpClientTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"cfo-knowledge-{Guid.NewGuid():N}");
    private readonly string knowledgeRoot;
    private readonly KnowledgeFileMcpClient client;

    public KnowledgeFileMcpClientTests()
    {
        knowledgeRoot = Path.Combine(root, "data", "knowledge");
        Directory.CreateDirectory(knowledgeRoot);
        client = new KnowledgeFileMcpClient(
            Options.Create(new McpOptions
            {
                KnowledgeFiles = new KnowledgeFileMcpOptions
                {
                    RootPath = Path.Combine("data", "knowledge"),
                    TimeoutSeconds = 5
                }
            }),
            new TestHostEnvironment(root),
            NullLogger<KnowledgeFileMcpClient>.Instance);
    }

    [Fact]
    public async Task ReadsAllowedFile()
    {
        await File.WriteAllTextAsync(Path.Combine(knowledgeRoot, "budget.md"), "approved content");

        var result = await client.ReadFileAsync("budget.md", CancellationToken.None);

        Assert.Equal("approved content", result);
    }

    [Fact]
    public async Task ListsAllowedFilesUsingRelativePaths()
    {
        Directory.CreateDirectory(Path.Combine(knowledgeRoot, "reports"));
        await File.WriteAllTextAsync(Path.Combine(knowledgeRoot, "z.md"), "z");
        await File.WriteAllTextAsync(Path.Combine(knowledgeRoot, "reports", "a.md"), "a");

        var result = await client.ListFilesAsync(CancellationToken.None);

        Assert.Equal(["reports/a.md", "z.md"], result);
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("..\\outside.md")]
    public async Task RejectsTraversal(string path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadFileAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsAbsolutePath()
    {
        var path = Path.Combine(knowledgeRoot, "inside.md");

        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadFileAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsPathOutsideKnowledgeDirectory()
    {
        var outsidePath = Path.Combine(root, "outside.md");
        await File.WriteAllTextAsync(outsidePath, "outside");

        await Assert.ThrowsAsync<ArgumentException>(() => client.ReadFileAsync(outsidePath, CancellationToken.None));
    }

    [Fact]
    public async Task MissingFileReturnsClearException()
    {
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            () => client.ReadFileAsync("missing.md", CancellationToken.None));

        Assert.Contains("does not exist", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "CfoAgent.Api.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
