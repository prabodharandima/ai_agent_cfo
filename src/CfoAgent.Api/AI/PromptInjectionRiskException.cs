namespace CfoAgent.Api.AI;

public sealed class PromptInjectionRiskException : Exception
{
    public PromptInjectionRiskException()
        : base("The request contains unsupported instruction content.")
    {
    }
}
