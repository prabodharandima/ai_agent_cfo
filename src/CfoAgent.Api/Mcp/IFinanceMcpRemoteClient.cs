namespace CfoAgent.Api.Mcp;

public interface IFinanceMcpRemoteClient : IFinanceMcpClient
{
    Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken);
}
