namespace CfoAgent.Api.Mcp;

public interface IKnowledgeFileMcpClient
{
    Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken);

    Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken);
}
