namespace CfoAgent.Api.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "AI";

    public string Provider { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public int SimulatedDelayMilliseconds { get; init; }

    public bool SimulateFailure { get; init; }
}
