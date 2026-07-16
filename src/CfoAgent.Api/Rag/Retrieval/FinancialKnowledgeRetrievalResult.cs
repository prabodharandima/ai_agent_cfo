namespace CfoAgent.Api.Rag.Retrieval;

public sealed record FinancialKnowledgeRetrievalResult(
    IReadOnlyList<FinancialKnowledgeSource> Sources,
    IReadOnlyList<string> Warnings)
{
    public bool HasSufficientKnowledge => Sources.Count > 0;
}
