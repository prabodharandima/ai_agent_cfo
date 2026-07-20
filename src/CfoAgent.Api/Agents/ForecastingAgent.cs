using CfoAgent.Api.AI;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Features.Forecasting;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.AI;

namespace CfoAgent.Api.Agents;

public sealed class ForecastingAgent(
    SalesForecastingService salesForecastingService,
    IChatClient chatClient,
    IFinanceMcpClient financeMcpClient)
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
            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, AgentPromptTemplates.ForForecast(forecast))],
                new ChatOptions { Instructions = AgentDefinitions.Forecasting.SystemInstructions },
                cancellationToken);

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
        catch (McpDependencyException)
        {
            throw;
        }
        catch (LlmDependencyException)
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
        var historical = await financeMcpClient.GetHistoricalYearlyTotalsAsync(cancellationToken);
        return salesForecastingService.Forecast(historical);
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
