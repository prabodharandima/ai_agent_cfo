using System.Text;
using CfoAgent.Api.AI;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Rag.Retrieval;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Agents;

public sealed class FinancialKnowledgeAgent(
    IFinancialKnowledgeSearch knowledgeSearch,
    IChatClient chatClient,
    IOptions<RagOptions> options)
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
            var retrieval = await knowledgeSearch.RetrieveAsync(query, cancellationToken);

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
                [new ChatMessage(ChatRole.User, AgentPromptTemplates.ForKnowledge(BuildBoundedContext(retrieval.Sources)))],
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

    private string BuildBoundedContext(IReadOnlyList<FinancialKnowledgeSource> sources)
    {
        var context = new StringBuilder();

        foreach (var source in sources)
        {
            var header = $"Source: {source.DocumentName} | Section: {source.Section} | Period: {source.Period} | Path: {source.SourcePath}\n";
            var separator = context.Length == 0 ? string.Empty : "\n\n";
            var remaining = _options.MaxKnowledgeContextCharacters - context.Length - separator.Length;
            if (remaining < header.Length)
            {
                break;
            }

            context.Append(separator).Append(header);
            remaining -= header.Length;
            var content = source.Content.Length <= remaining ? source.Content : source.Content[..remaining];
            context.Append(content);

            if (content.Length < source.Content.Length)
            {
                break;
            }
        }

        return context.ToString();
    }
}
