namespace CfoAgent.Api.Rag.Retrieval;

public interface IFinancialKnowledgeSearch
{
    Task<FinancialKnowledgeRetrievalResult> RetrieveAsync(
        FinancialKnowledgeQuery query,
        CancellationToken cancellationToken = default);
}
