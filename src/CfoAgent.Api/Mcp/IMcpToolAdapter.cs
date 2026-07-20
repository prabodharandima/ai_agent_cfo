using System.Text.Json;

namespace CfoAgent.Api.Mcp;

public interface IMcpToolAdapter
{
    Task<IReadOnlyList<string>> GetApprovedToolNamesAsync(
        IEnumerable<string>? requiredToolNames,
        CancellationToken cancellationToken);

    Task<JsonElement> CallApprovedToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments,
        CancellationToken cancellationToken);
}
