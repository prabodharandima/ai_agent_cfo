using CfoAgent.Api.Agents.Contracts;

namespace CfoAgent.Api.Agents;

public sealed class AgentResultComposer
{
    public AgentResult Compose(IReadOnlyList<AgentResult> specialistResults)
    {
        ArgumentNullException.ThrowIfNull(specialistResults);

        if (specialistResults.Count == 0)
        {
            throw new ArgumentException("At least one specialist result is required.", nameof(specialistResults));
        }

        if (specialistResults.Count == 1)
        {
            return specialistResults[0];
        }

        var structuredData = specialistResults
            .Select(result => new OrchestratedSpecialistResult(
                result.AgentNames.Single(),
                result.ResponseType,
                result.StructuredData))
            .ToArray();

        return new AgentResult(
            string.Join("\n\n", specialistResults.Select(result => result.Answer)),
            AgentResponseType.Mixed,
            specialistResults.SelectMany(result => result.AgentNames).Distinct(StringComparer.Ordinal).ToArray(),
            structuredData,
            specialistResults.SelectMany(result => result.Sources).Distinct().ToArray(),
            specialistResults.SelectMany(result => result.Assumptions).Distinct(StringComparer.Ordinal).ToArray(),
            specialistResults.SelectMany(result => result.Warnings).Distinct(StringComparer.Ordinal).ToArray(),
            specialistResults.Select(result => result.DataPeriod).FirstOrDefault(period => period is not null));
    }
}
