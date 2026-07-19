namespace CfoAgent.Api.Configuration;

public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    public FinanceMcpOptions Finance { get; init; } = new();

    public KnowledgeFileMcpOptions KnowledgeFiles { get; init; } = new();
}

public sealed class FinanceMcpOptions
{
    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; }
}

public sealed class KnowledgeFileMcpOptions
{
    public bool Enabled { get; init; }

    public string BaseUrl { get; init; } = string.Empty;

    public string RootPath { get; init; } = string.Empty;

    public bool UseLocalFallback { get; init; }

    public int TimeoutSeconds { get; init; }
}
