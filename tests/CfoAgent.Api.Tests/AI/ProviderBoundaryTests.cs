using CfoAgent.Api.Agents;
using CfoAgent.Api.Features.Chat;

namespace CfoAgent.Api.Tests.AI;

public sealed class ProviderBoundaryTests
{
    [Fact]
    public void ChatEndpointSource_DoesNotHardCodeOllama()
    {
        var repositoryRoot = FindRepositoryRoot();
        var endpointSource = File.ReadAllText(Path.Combine(repositoryRoot, "src", "CfoAgent.Api", "Features", "Chat", "ChatEndpoints.cs"));

        Assert.DoesNotContain("Ollama", endpointSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplicationAndEndpointTypesDoNotExposeOllamaTypes()
    {
        var types = new[]
        {
            typeof(ChatEndpoints),
            typeof(CfoOrchestratorAgent),
            typeof(SalesAnalysisAgent),
            typeof(ForecastingAgent),
            typeof(FinancialKnowledgeAgent),
            typeof(AgentResultComposer)
        };

        var referencedTypeNames = types
            .SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters()))
            .Concat(types.SelectMany(type => type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .SelectMany(method => method.GetParameters())))
            .Select(parameter => parameter.ParameterType.FullName ?? string.Empty);

        Assert.DoesNotContain(referencedTypeNames, name => name.StartsWith("CfoAgent.Api.AI.Ollama.", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CfoAgent.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
