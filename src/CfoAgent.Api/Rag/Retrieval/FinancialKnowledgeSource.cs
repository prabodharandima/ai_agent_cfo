using CfoAgent.Api.Agents.Contracts;

namespace CfoAgent.Api.Rag.Retrieval;

public sealed record FinancialKnowledgeSource(
    string ChunkId,
    string DocumentId,
    string DocumentName,
    string DocumentType,
    string Period,
    string Section,
    string SourcePath,
    string Content,
    float Distance)
{
    public AgentSource ToAgentSource() => new(DocumentId, DocumentName, Section, SourcePath, Period);
}
