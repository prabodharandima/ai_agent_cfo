namespace CfoAgent.Api.Tests.PhaseEight;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ContainerIntegrationFactAttribute : FactAttribute
{
    public ContainerIntegrationFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("CFO_CONTAINER_GATE"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Container integration tests run only through scripts/test-phase-8-containers.ps1.";
        }
    }
}

