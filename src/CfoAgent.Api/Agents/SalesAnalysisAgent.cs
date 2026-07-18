using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using Microsoft.Agents.AI;

namespace CfoAgent.Api.Agents;

public sealed class SalesAnalysisAgent(
    SalesAnalysisService salesAnalysisService,
    CfoAgentFramework agentFramework,
    IFinanceMcpClient? financeMcpClient = null,
    FinanceMcpFallback? financeMcpFallback = null)
{
    public async Task<AgentResult> GetWeeklySummaryAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        try
        {
            var summary = await ExecuteFinanceAsync(
                token => financeMcpClient!.GetCurrentWeekSummaryAsync(token),
                salesAnalysisService.GetCurrentWeekSummaryAsync,
                cancellationToken);
            var answer = await GetAnswerAsync(AgentPromptTemplates.ForSalesSummary(summary), cancellationToken);

            return new AgentResult(
                answer,
                AgentResponseType.SalesSummary,
                [AgentDefinitions.SalesAnalysis.Name],
                summary,
                Array.Empty<AgentSource>(),
                Array.Empty<string>(),
                summary.Warnings,
                ToDataPeriod(summary.Period, "Current week"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("The sales analysis agent could not produce a weekly summary.", exception);
        }
    }

    public async Task<AgentResult> GetWeekOverWeekComparisonAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        try
        {
            var comparison = await ExecuteFinanceAsync(
                token => financeMcpClient!.GetWeekOverWeekComparisonAsync(token),
                salesAnalysisService.GetWeekOverWeekComparisonAsync,
                cancellationToken);
            var answer = await GetAnswerAsync(AgentPromptTemplates.ForSalesComparison(comparison), cancellationToken);
            var warnings = comparison.CurrentWeek.Warnings
                .Concat(comparison.PreviousWeek.Warnings)
                .Concat(comparison.Warnings)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return new AgentResult(
                answer,
                AgentResponseType.SalesComparison,
                [AgentDefinitions.SalesAnalysis.Name],
                comparison,
                Array.Empty<AgentSource>(),
                Array.Empty<string>(),
                warnings,
                ToDataPeriod(comparison.CurrentWeek.Period, "Current week"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("The sales analysis agent could not produce a weekly comparison.", exception);
        }
    }

    public async Task<AgentResult> GetCurrentMonthTopProductsAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        try
        {
            var topProducts = await ExecuteFinanceAsync(
                token => financeMcpClient!.GetCurrentMonthTopProductsAsync(token),
                salesAnalysisService.GetCurrentMonthTopProductsAsync,
                cancellationToken);
            var answer = await GetAnswerAsync(AgentPromptTemplates.ForTopProducts(topProducts), cancellationToken);

            return new AgentResult(
                answer,
                AgentResponseType.TopProducts,
                [AgentDefinitions.SalesAnalysis.Name],
                topProducts,
                Array.Empty<AgentSource>(),
                Array.Empty<string>(),
                topProducts.Warnings,
                ToDataPeriod(topProducts.Period, "Current month"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("The sales analysis agent could not produce a top-products result.", exception);
        }
    }

    private async Task<string> GetAnswerAsync(string prompt, CancellationToken cancellationToken)
    {
        var agent = agentFramework.CreateAgent(AgentDefinitions.SalesAnalysis);
        var session = await agent.CreateSessionAsync(cancellationToken);
        var response = await agent.RunAsync(prompt, session, options: null, cancellationToken);

        return response.Text;
    }

    private async Task<T> ExecuteFinanceAsync<T>(
        Func<CancellationToken, Task<T>> mcpOperation,
        Func<CancellationToken, Task<T>> localOperation,
        CancellationToken cancellationToken)
    {
        if (financeMcpClient is null || financeMcpFallback is null)
        {
            return await localOperation(cancellationToken);
        }

        var result = await financeMcpFallback.ExecuteAsync(mcpOperation, localOperation, cancellationToken);
        return result.Value;
    }

    private static AgentDataPeriod ToDataPeriod(SalesPeriod period, string label) => new(period.StartDate, period.EndDate, label);

    private static void ValidateRequest(AgentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Agent requests require a message.", nameof(request));
        }
    }
}
