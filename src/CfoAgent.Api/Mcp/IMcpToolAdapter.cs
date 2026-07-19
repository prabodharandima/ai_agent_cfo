using System.Text.Json;
using ModelContextProtocol.Client;

namespace CfoAgent.Api.Mcp;

public interface IMcpToolAdapter
{
    Task<IReadOnlyList<McpClientTool>> GetApprovedToolsAsync(
        IEnumerable<string>? operationToolNames,
        CancellationToken cancellationToken);

    Task<JsonElement> CallApprovedToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken);
}
