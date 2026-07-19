namespace CfoAgent.Api.Mcp;

public sealed class KnowledgeFileMcpAccess(
    IKnowledgeFileMcpRemoteClient remoteClient,
    KnowledgeFileMcpClient localClient,
    KnowledgeFileMcpFallback fallback) : IKnowledgeFileMcpClient
{
    public async Task<IReadOnlyList<string>> ListFilesAsync(CancellationToken cancellationToken)
    {
        var result = await fallback.ExecuteAsync(
            remoteClient.ListFilesAsync,
            localClient.ListFilesAsync,
            cancellationToken);
        return result.Value;
    }

    public async Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        KnowledgeFileMcpHttpClient.ValidateRelativePath(relativePath);
        var result = await fallback.ExecuteAsync(
            token => remoteClient.ReadFileAsync(relativePath, token),
            token => localClient.ReadFileAsync(relativePath, token),
            cancellationToken);
        return result.Value;
    }
}
