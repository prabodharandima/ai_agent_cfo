using CfoAgent.Api.AI;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.AI;
using System.Globalization;
using System.Text.Json;

namespace CfoAgent.Api.Agents;

public sealed class SalesAnalysisAgent(
    IChatClient chatClient,
    IFinanceMcpClient financeMcpClient,
    TimeProvider? timeProvider = null)
{
    private const int MaximumPeriodDays = 366;
    private const int MaximumDateRangeResponseCharacters = 256;
    private static readonly JsonSerializerOptions StructuredOutputJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<AgentResult> GetWeeklySummaryAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        try
        {
            var currentDate = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
            var summary = IsCurrentWeekRequest(request.Message)
                ? await financeMcpClient.GetCurrentWeekSummaryAsync(cancellationToken)
                : await financeMcpClient.GetSalesSummaryAsync(
                    await ResolveSalesSummaryPeriodAsync(request.Message, currentDate, cancellationToken),
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
                ToDataPeriod(summary.Period, GetPeriodLabel(summary.Period, currentDate)));
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
            throw new InvalidOperationException("The sales analysis agent could not produce a weekly summary.", exception);
        }
    }

    public async Task<AgentResult> GetWeekOverWeekComparisonAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        try
        {
            var comparison = await financeMcpClient.GetWeekOverWeekComparisonAsync(cancellationToken);
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
            throw new InvalidOperationException("The sales analysis agent could not produce a weekly comparison.", exception);
        }
    }

    public async Task<AgentResult> GetCurrentMonthTopProductsAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        try
        {
            var topProducts = await financeMcpClient.GetCurrentMonthTopProductsAsync(cancellationToken);
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
            throw new InvalidOperationException("The sales analysis agent could not produce a top-products result.", exception);
        }
    }

    private async Task<string> GetAnswerAsync(string prompt, CancellationToken cancellationToken)
    {
        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)],
            new ChatOptions { Instructions = AgentDefinitions.SalesAnalysis.SystemInstructions },
            cancellationToken);

        return response.Text;
    }

    private async Task<SalesPeriod> ResolveSalesSummaryPeriodAsync(
        string message,
        DateOnly currentDate,
        CancellationToken cancellationToken)
    {
        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, AgentPromptTemplates.ForSalesSummaryDateRange(message, currentDate))],
            new ChatOptions
            {
                Instructions = AgentDefinitions.SalesAnalysis.SystemInstructions,
                ResponseFormat = ChatResponseFormat.ForJsonSchema<SalesSummaryDateRangeOutput>(
                    StructuredOutputJsonOptions,
                    "sales_summary_date_range",
                    "An inclusive, validated sales-summary date range.")
            },
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Text) || response.Text.Length > MaximumDateRangeResponseCharacters)
        {
            throw InvalidStructuredDateRange();
        }

        try
        {
            var output = JsonSerializer.Deserialize<SalesSummaryDateRangeOutput>(response.Text, StructuredOutputJsonOptions);
            if (output is null ||
                !DateOnly.TryParseExact(output.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate) ||
                !DateOnly.TryParseExact(output.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
            {
                throw InvalidStructuredDateRange();
            }

            if (startDate > currentDate || endDate > currentDate || endDate < startDate || endDate.DayNumber - startDate.DayNumber + 1 > MaximumPeriodDays)
            {
                throw InvalidStructuredDateRange();
            }

            return new SalesPeriod(startDate, endDate);
        }
        catch (JsonException)
        {
            throw InvalidStructuredDateRange();
        }
    }

    private AiProviderException InvalidStructuredDateRange() =>
        new(
            (chatClient.GetService(typeof(ChatClientMetadata)) as ChatClientMetadata)?.ProviderName ?? "AI",
            AiProviderFailureKind.InvalidResponse);

    private static string GetPeriodLabel(SalesPeriod period, DateOnly currentDate) =>
        period == new SalesPeriod(StartOfWeek(currentDate), currentDate) ? "Current week" : "Requested period";

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);

    private static bool IsCurrentWeekRequest(string message) =>
        message.Contains("this week", StringComparison.OrdinalIgnoreCase)
        || message.Contains("current week", StringComparison.OrdinalIgnoreCase);

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
