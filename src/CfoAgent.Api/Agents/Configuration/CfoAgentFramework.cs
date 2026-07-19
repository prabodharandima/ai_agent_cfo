using System.Text.Json;
using System.Text.Json.Nodes;
using CfoAgent.Api.Mcp;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace CfoAgent.Api.Agents.Configuration;

public sealed class CfoAgentFramework(IChatClient chatClient, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ChatClientAgent CreateAgent(AgentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Id = definition.Name,
                Name = definition.Name,
                Description = definition.Description,
                ChatOptions = new ChatOptions { Instructions = definition.SystemInstructions },
                UseProvidedChatClientAsIs = true
            },
            loggerFactory,
            serviceProvider);
    }

    public async Task<FunctionCallContent> SelectMcpToolAsync(
        string dependencyName,
        string userMessage,
        IReadOnlyList<McpClientTool> approvedTools,
        IReadOnlyDictionary<string, object?> canonicalArguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        ArgumentNullException.ThrowIfNull(approvedTools);
        ArgumentNullException.ThrowIfNull(canonicalArguments);

        if (approvedTools.Count == 0)
        {
            throw new McpDependencyException(dependencyName, McpDependencyFailureKind.CapabilityMismatch);
        }

        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, AgentPromptTemplates.ForMcpToolSelection(userMessage, canonicalArguments))],
            new ChatOptions
            {
                Tools = approvedTools.Cast<AITool>().ToList(),
                ToolMode = ChatToolMode.RequireAny,
                Temperature = 0
            },
            cancellationToken);
        var calls = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .ToArray();

        if (calls.Length != 1 || calls[0].Exception is not null)
        {
            throw new McpDependencyException(dependencyName, McpDependencyFailureKind.InvalidResponse);
        }

        var selected = calls[0];
        if (!approvedTools.Any(tool => string.Equals(tool.Name, selected.Name, StringComparison.Ordinal)))
        {
            throw new McpDependencyException(dependencyName, McpDependencyFailureKind.CapabilityMismatch);
        }

        if (!ArgumentsMatch(selected.Arguments, canonicalArguments))
        {
            throw new McpDependencyException(dependencyName, McpDependencyFailureKind.InvalidResponse);
        }

        return selected;
    }

    private static bool ArgumentsMatch(
        IDictionary<string, object?>? selectedArguments,
        IReadOnlyDictionary<string, object?> canonicalArguments)
    {
        if (selectedArguments is null || selectedArguments.Count != canonicalArguments.Count)
        {
            return selectedArguments is null && canonicalArguments.Count == 0;
        }

        foreach (var expected in canonicalArguments)
        {
            if (!selectedArguments.TryGetValue(expected.Key, out var actualValue))
            {
                return false;
            }

            var actualNode = JsonSerializer.SerializeToNode(actualValue, JsonOptions);
            var expectedNode = JsonSerializer.SerializeToNode(expected.Value, JsonOptions);
            if (!JsonNode.DeepEquals(actualNode, expectedNode))
            {
                return false;
            }
        }

        return true;
    }
}
