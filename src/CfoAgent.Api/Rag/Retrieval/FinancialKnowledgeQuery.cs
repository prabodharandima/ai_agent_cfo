namespace CfoAgent.Api.Rag.Retrieval;

public sealed record FinancialKnowledgeQuery(
    string Query,
    int TopK = 3,
    string? DocumentType = null,
    string? Period = null);
