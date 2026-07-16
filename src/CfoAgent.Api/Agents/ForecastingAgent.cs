using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Mcp;

namespace CfoAgent.Api.Agents;

public sealed class ForecastingAgent(
    SalesForecastingService salesForecastingService,
    CfoAgentFramework agentFramework,
    IFinanceMcpClient? financeMcpClient = null,
    FinanceMcpFallback? financeMcpFallback = null)
{
    public async Task<AgentResult> GetForecastAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Agent requests require a message.", nameof(request));
        }

        try
        {
            var forecast = await GetForecastAsync(cancellationToken);
            var agent = agentFramework.CreateAgent(AgentDefinitions.Forecasting);
            var session = await agent.CreateSessionAsync(cancellationToken);
            var response = await agent.RunAsync(AgentPromptTemplates.ForForecast(forecast), session, options: null, cancellationToken);

            return new AgentResult(
                response.Text,
                AgentResponseType.Forecast,
                [AgentDefinitions.Forecasting.Name],
                forecast,
                Array.Empty<AgentSource>(),
                forecast.Assumptions,
                forecast.Warnings,
                ToDataPeriod(forecast));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("The forecasting agent could not produce a forecast.", exception);
        }
    }

    private async Task<SalesForecastResult> GetForecastAsync(CancellationToken cancellationToken)
    {
        if (financeMcpClient is null || financeMcpFallback is null)
        {
            return await salesForecastingService.ForecastAsync(cancellationToken);
        }

        var historical = await financeMcpFallback.ExecuteAsync(
            financeMcpClient.GetHistoricalYearlyTotalsAsync,
            salesForecastingService.GetHistoricalYearlyTotalsAsync,
            cancellationToken);

        return salesForecastingService.Forecast(historical.Value);
    }

    private static AgentDataPeriod? ToDataPeriod(SalesForecastResult forecast)
    {
        if (forecast.HistoricalPeriodStartYear is not int startYear || forecast.HistoricalPeriodEndYear is not int endYear)
        {
            return null;
        }

        return new AgentDataPeriod(new DateOnly(startYear, 1, 1), new DateOnly(endYear, 12, 31), "Historical sales");
    }
}
