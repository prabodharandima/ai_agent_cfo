namespace CfoAgent.Api.Mcp;

public sealed class KnowledgeFileMcpAccess(
    IKnowledgeFileMcpProcessClient processClient,
    KnowledgeFileMcpClient localClient,
    KnowledgeFileMcpFallback fallback) : IKnowledgeFileMcpClient
{
    public async Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken)
    {
        var result = await fallback.ExecuteAsync(
            processClient.ListFilesAsync,
            localClient.ListFilesAsync,
            cancellationToken);
        return result.Value;
    }

    public async Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        KnowledgeFileMcpProcessClient.ValidateRelativePath(relativePath);
        var result = await fallback.ExecuteAsync(
            token => processClient.ReadFileAsync(relativePath, token),
            token => localClient.ReadFileAsync(relativePath, token),
            cancellationToken);
        return result.Value;
    }
}
