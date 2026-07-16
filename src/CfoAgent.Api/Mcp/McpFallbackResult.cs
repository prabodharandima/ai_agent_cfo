namespace CfoAgent.Api.Mcp;

public sealed record McpFallbackResult<T>(T Value, bool UsedFallback, string? FallbackReason);
