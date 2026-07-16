using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CfoAgent.Api.Agents.Configuration;

public sealed class CfoAgentFramework(IChatClient chatClient, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
{
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
}
