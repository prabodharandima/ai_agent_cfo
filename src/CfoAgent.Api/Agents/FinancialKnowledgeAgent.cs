using System.Text;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Mcp;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Agents;

public sealed class FinancialKnowledgeAgent(
    FinancialKnowledgeRetrievalService retrievalService,
    CfoAgentFramework agentFramework,
    IOptions<RagOptions> options,
    IKnowledgeFileMcpClient? knowledgeFileMcpClient = null)
{
    private readonly RagOptions _options = options.Value;

    public async Task<AgentResult> AnswerAsync(
        AgentRequest request,
        int topK = 3,
        string? documentType = null,
        string? period = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Message);

        try
        {
            var query = new FinancialKnowledgeQuery(request.Message, topK, documentType, period);
            var retrieval = await RetrieveAsync(query, cancellationToken);

            if (!retrieval.HasSufficientKnowledge)
            {
                return new AgentResult(
                    "Insufficient financial knowledge is available to answer this question from the indexed documents.",
                    AgentResponseType.Knowledge,
                    [AgentDefinitions.FinancialKnowledge.Name],
                    retrieval,
                    Array.Empty<AgentSource>(),
                    Array.Empty<string>(),
                    retrieval.Warnings,
                    null);
            }

            var agent = agentFramework.CreateAgent(AgentDefinitions.FinancialKnowledge);
            var session = await agent.CreateSessionAsync(cancellationToken);
            var response = await agent.RunAsync(AgentPromptTemplates.ForKnowledge(BuildBoundedContext(retrieval.Sources)), session, options: null, cancellationToken);

            return new AgentResult(
                response.Text,
                AgentResponseType.Knowledge,
                [AgentDefinitions.FinancialKnowledge.Name],
                retrieval,
                retrieval.Sources.Select(source => source.ToAgentSource())
                    .DistinctBy(source => (source.DocumentId, source.Section, source.SourcePath))
                    .ToArray(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("The financial knowledge agent could not retrieve an answer.", exception);
        }
    }

    private async Task<FinancialKnowledgeRetrievalResult> RetrieveAsync(
        FinancialKnowledgeQuery query,
        CancellationToken cancellationToken)
    {
        if (knowledgeFileMcpClient is not null)
        {
            await knowledgeFileMcpClient.ListFilesAsync(cancellationToken);
        }

        return await retrievalService.RetrieveAsync(query, cancellationToken);
    }

    private string BuildBoundedContext(IReadOnlyList<FinancialKnowledgeSource> sources)
    {
        var context = new StringBuilder();

        foreach (var source in sources)
        {
            var header = $"Source: {source.DocumentName} | Section: {source.Section} | Period: {source.Period} | Path: {source.SourcePath}\n";
            var remaining = _options.MaxKnowledgeContextCharacters - context.Length - header.Length;
            if (remaining <= 0)
            {
                break;
            }

            var content = source.Content.Length <= remaining ? source.Content : source.Content[..remaining];
            context.Append(header).AppendLine(content).AppendLine();
        }

        return context.ToString().TrimEnd();
    }
}
