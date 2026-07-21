using System.Globalization;
using System.Text.Json;
using CfoAgent.Api.AI;
using CfoAgent.Api.Agents.Configuration;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Features.Sales;
using CfoAgent.Api.Mcp;
using Microsoft.Extensions.AI;

namespace CfoAgent.Api.Agents;

public sealed class SalesAnalysisAgent(
    IChatClient chatClient,
    IFinanceMcpClient financeMcpClient,
    TimeProvider? timeProvider = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentResult> GetWeeklySummaryAsync(AgentRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        try
        {
            var currentDate = DateOnly.FromDateTime((timeProvider ?? TimeProvider.System).GetLocalNow().DateTime);
            var requestedPeriod = await ResolveSalesSummaryPeriodAsync(request.Message, currentDate, cancellationToken);
            var summary = await financeMcpClient.GetSalesSummaryAsync(requestedPeriod, cancellationToken);
            var answer = await GetAnswerAsync(AgentPromptTemplates.ForSalesSummary(summary), cancellationToken);

            return new AgentResult(
                answer,
                AgentResponseType.SalesSummary,
                [AgentDefinitions.SalesAnalysis.Name],
                summary,
                Array.Empty<AgentSource>(),
                Array.Empty<string>(),
                summary.Warnings,
                ToDataPeriod(summary.Period, "Requested period"));
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
            throw new InvalidOperationException("The sales analysis agent could not produce a sales summary.", exception);
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
            new ChatOptions { Instructions = AgentDefinitions.SalesAnalysis.SystemInstructions },
            cancellationToken);

        if (TryResolveStandardRelativePeriod(message, currentDate, out var standardPeriod))
        {
            return standardPeriod;
        }

        var resolvedRange = DeserializeDateRange(response.Text);
        return ValidateDateRange(resolvedRange, currentDate);
    }

    private static SalesSummaryDateRange DeserializeDateRange(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException("The model did not return a sales-summary date range.");
        }

        try
        {
            return JsonSerializer.Deserialize<SalesSummaryDateRange>(responseText, JsonOptions)
                ?? throw new InvalidOperationException("The model returned an invalid sales-summary date range.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The model returned an invalid sales-summary date range.", exception);
        }
    }

    private static SalesPeriod ValidateDateRange(SalesSummaryDateRange range, DateOnly currentDate)
    {
        if (!DateOnly.TryParseExact(range.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate) ||
            !DateOnly.TryParseExact(range.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
        {
            throw new InvalidOperationException("The model returned dates in an invalid format.");
        }

        if (endDate < startDate)
        {
            throw new InvalidOperationException("The model returned a sales-summary range with an end date before its start date.");
        }

        if (endDate > currentDate)
        {
            throw new InvalidOperationException("The model returned a sales-summary range in the future.");
        }

        return new SalesPeriod(startDate, endDate);
    }

    private static bool TryResolveStandardRelativePeriod(string message, DateOnly currentDate, out SalesPeriod period)
    {
        var normalized = message.ToUpperInvariant();

        if (normalized.Contains("SINCE YESTERDAY", StringComparison.Ordinal))
        {
            period = new SalesPeriod(currentDate.AddDays(-1), currentDate);
            return true;
        }

        if (normalized.Contains("LAST WEEK", StringComparison.Ordinal))
        {
            var currentWeekStart = StartOfWeek(currentDate);
            period = new SalesPeriod(currentWeekStart.AddDays(-7), currentWeekStart.AddDays(-1));
            return true;
        }

        if (normalized.Contains("THIS WEEK", StringComparison.Ordinal))
        {
            period = new SalesPeriod(StartOfWeek(currentDate), currentDate);
            return true;
        }

        if (normalized.Contains("YESTERDAY", StringComparison.Ordinal))
        {
            var yesterday = currentDate.AddDays(-1);
            period = new SalesPeriod(yesterday, yesterday);
            return true;
        }

        if (normalized.Contains("TODAY", StringComparison.Ordinal))
        {
            period = new SalesPeriod(currentDate, currentDate);
            return true;
        }

        period = null!;
        return false;
    }

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);

    private static AgentDataPeriod ToDataPeriod(SalesPeriod period, string label) => new(period.StartDate, period.EndDate, label);

    private static void ValidateRequest(AgentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Agent requests require a message.", nameof(request));
        }
    }

    private sealed record SalesSummaryDateRange(string? StartDate, string? EndDate);
}
