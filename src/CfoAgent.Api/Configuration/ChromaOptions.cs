namespace CfoAgent.Api.Configuration;

public sealed class ChromaOptions
{
    public const string SectionName = "Chroma";

    public string BaseUrl { get; init; } = string.Empty;

    public string CollectionName { get; init; } = string.Empty;

    public string Tenant { get; init; } = "default_tenant";

    public string Database { get; init; } = "default_database";

    public int TimeoutSeconds { get; init; }
}
