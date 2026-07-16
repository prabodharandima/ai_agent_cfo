namespace CfoAgent.Api.Mcp;

public interface IKnowledgeFileMcpProcessClient
{
    Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken);

    Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken);
}
