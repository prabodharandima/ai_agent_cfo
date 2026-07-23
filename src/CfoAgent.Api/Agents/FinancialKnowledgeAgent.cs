using CfoAgent.Api.AI;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.Extensions.AI;

namespace CfoAgent.Api.Agents;

public sealed class FinancialKnowledgeAgent(
    FinancialKnowledgeContextProvider knowledgeContextProvider,
    IChatClient chatClient)
{
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
            var knowledgeContext = await knowledgeContextProvider.PrepareAsync(query, cancellationToken);
            var retrieval = knowledgeContext.Retrieval;

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

            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, AgentPromptTemplates.ForKnowledge(knowledgeContext.BoundedContext))],
                new ChatOptions { Instructions = AgentDefinitions.FinancialKnowledge.SystemInstructions },
                cancellationToken);

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
        catch (VectorSearchDependencyException)
        {
            throw;
        }
        catch (LlmDependencyException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("The financial knowledge agent could not retrieve an answer.", exception);
        }
    }

}
