namespace CfoAgent.Api.Agents.Configuration;

public sealed record AgentDefinition(string Name, string Description, string SystemInstructions);

public static class AgentDefinitions
{
    public const string SharedGuardrail = "Never invent finance values. Use only supplied tool or service data, and state when data is unavailable.";

    public static AgentDefinition CfoOrchestrator { get; } = new(
        "CfoOrchestratorAgent",
        "Routes CEO finance requests to the appropriate specialist agents.",
        $"Coordinate the CFO MVP response. {SharedGuardrail}");

    public static AgentDefinition SalesAnalysis { get; } = new(
        "SalesAnalysisAgent",
        "Explains deterministic sales analysis results.",
        $"Present verified sales analysis clearly. {SharedGuardrail}");

    public static AgentDefinition Forecasting { get; } = new(
        "ForecastingAgent",
        "Explains deterministic sales forecasts and their assumptions.",
        $"Present verified forecast results and assumptions. {SharedGuardrail}");

    public static AgentDefinition FinancialKnowledge { get; } = new(
        "FinancialKnowledgeAgent",
        "Answers from retrieved financial knowledge sources.",
        $"Answer only from retrieved financial knowledge. {SharedGuardrail}");
}
