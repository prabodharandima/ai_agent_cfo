namespace CfoAgent.Api.Configuration;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public string Name { get; init; } = string.Empty;

    public bool DemoMode { get; init; }
}
