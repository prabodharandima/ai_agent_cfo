namespace CfoAgent.Api.Agents.Configuration;

public sealed record AgentDefinition(string Name, string Description, string SystemInstructions);

public static class AgentDefinitions
{
    public const string SharedGuardrail = "Use only supplied verified data. Never calculate, alter, infer, or invent finance values. Never request or invoke tools. State when supplied data is insufficient.";

    public static AgentDefinition CfoOrchestrator { get; } = new(
        "CfoOrchestratorAgent",
        "Routes CEO finance requests to the appropriate specialist agents.",
        $"Classify only into the allowed CFO intents and combine only supplied specialist results. Keep responses concise. {SharedGuardrail}");

    public static AgentDefinition SalesAnalysis { get; } = new(
        "SalesAnalysisAgent",
        "Explains deterministic sales analysis results.",
        $"Present verified sales analysis in concise executive language. {SharedGuardrail}");

    public static AgentDefinition Forecasting { get; } = new(
        "ForecastingAgent",
        "Explains deterministic sales forecasts and their assumptions.",
        $"Present verified forecast results and assumptions in concise executive language. {SharedGuardrail}");

    public static AgentDefinition FinancialKnowledge { get; } = new(
        "FinancialKnowledgeAgent",
        "Answers from retrieved financial knowledge sources.",
        $"Answer concisely only from retrieved financial knowledge. {SharedGuardrail}");
}
