namespace CfoAgent.Api.Configuration;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string AllowedOrigin { get; init; } = string.Empty;
}
