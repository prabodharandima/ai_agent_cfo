using System.Globalization;
using System.Text.Json;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Features.Sales;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CfoAgent.Api.Mcp;

public sealed class FinanceMcpClient(
    IOptions<McpOptions> options,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    ILogger<FinanceMcpClient> logger) : IFinanceMcpRemoteClient, IAsyncDisposable
{
    public const string HttpClientName = "FinanceMcp";
    private const string DependencyName = "Finance MCP";
    private static readonly string[] RequiredTools = ["get_sales_summary", "compare_sales_periods", "get_top_products", "get_historical_sales", "get_budget_target"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private McpClient? client;

    public async Task<SalesSummary> GetCurrentWeekSummaryAsync(CancellationToken cancellationToken)
    {
        var currentDate = GetCurrentDate();
        var result = await CallToolAsync<McpSalesSummary>(
            "get_sales_summary",
            new Dictionary<string, object?>
            {
                ["startDate"] = FormatDate(StartOfWeek(currentDate)),
                ["endDate"] = FormatDate(currentDate)
            },
            cancellationToken);

        return ToSalesSummary(result);
    }

    public async Task<WeeklySalesComparison> GetWeekOverWeekComparisonAsync(CancellationToken cancellationToken)
    {
        var currentDate = GetCurrentDate();
        var currentStart = StartOfWeek(currentDate);
        var result = await CallToolAsync<McpPeriodComparison>(
            "compare_sales_periods",
            new Dictionary<string, object?>
            {
                ["currentStartDate"] = FormatDate(currentStart),
                ["currentEndDate"] = FormatDate(currentDate),
                ["previousStartDate"] = FormatDate(currentStart.AddDays(-7)),
                ["previousEndDate"] = FormatDate(currentStart.AddDays(-1))
            },
            cancellationToken);

        return new WeeklySalesComparison(
            ToSalesSummary(result.CurrentPeriod),
            ToSalesSummary(result.PreviousPeriod),
            result.NetRevenueChange,
            result.NetRevenueChangePercentage,
            ParseDirection(result.Direction),
            result.Warnings);
    }

    public async Task<TopProductsResult> GetCurrentMonthTopProductsAsync(CancellationToken cancellationToken)
    {
        var currentDate = GetCurrentDate();
        var result = await CallToolAsync<McpTopProducts>(
            "get_top_products",
            new Dictionary<string, object?>
            {
                ["startDate"] = FormatDate(new DateOnly(currentDate.Year, currentDate.Month, 1)),
                ["endDate"] = FormatDate(currentDate),
                ["limit"] = 5
            },
            cancellationToken);

        return new TopProductsResult(ToSalesPeriod(result.Period), result.Products.Select(ToTopProduct).ToArray(), result.Warnings);
    }

    public async Task<HistoricalYearlySalesResult> GetHistoricalYearlyTotalsAsync(CancellationToken cancellationToken)
    {
        var endYear = GetCurrentDate().Year - 1;
        var result = await CallToolAsync<McpHistoricalSales>(
            "get_historical_sales",
            new Dictionary<string, object?>
            {
                ["startYear"] = endYear - 4,
                ["endYear"] = endYear
            },
            cancellationToken);

        return new HistoricalYearlySalesResult(
            result.Totals.Select(total => new YearlySalesTotal(total.Year, total.NetRevenue)).ToArray(),
            result.Warnings);
    }

    public async Task<BudgetTargetResult> GetBudgetTargetAsync(int year, int? month, CancellationToken cancellationToken)
    {
        var result = await CallToolAsync<McpBudgetTarget>(
            "get_budget_target",
            new Dictionary<string, object?> { ["year"] = year, ["month"] = month },
            cancellationToken);
        return new BudgetTargetResult(
            result.Year,
            result.Month,
            result.IsAvailable,
            result.SalesTarget,
            result.ProfitTarget,
            result.AssumptionReference,
            result.Warnings);
    }

    public Task<IReadOnlyList<string>> DiscoverToolsAsync(CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return ExecuteDependencyOperationAsync(async token =>
        {
            var connectedClient = await GetClientAsync(token);
            var names = (await connectedClient.ListToolsAsync(cancellationToken: token))
                .Select(tool => tool.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var missing = RequiredTools.Except(names, StringComparer.Ordinal).Any();
            var unexpected = names.Except(RequiredTools, StringComparer.Ordinal).Any();
            if (missing || unexpected)
            {
                throw new McpDependencyException(DependencyName, McpDependencyFailureKind.CapabilityMismatch);
            }

            logger.LogInformation("Finance MCP capability discovery succeeded with {ToolCount} tools.", names.Length);
            return (IReadOnlyList<string>)names;
        }, cancellationToken);
    }

    private async Task<T> CallToolAsync<T>(
        string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        await DiscoverToolsAsync(cancellationToken);
        return await ExecuteDependencyOperationAsync(async token =>
        {
            var connectedClient = await GetClientAsync(token);
            var result = await connectedClient.CallToolAsync(toolName, arguments, cancellationToken: token);
            if (result.IsError == true)
            {
                throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse);
            }

            var content = result.Content.OfType<TextContentBlock>().SingleOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse);
            }

            FinanceMcpResponse<T>? response;
            try
            {
                response = JsonSerializer.Deserialize<FinanceMcpResponse<T>>(content, JsonOptions);
            }
            catch (JsonException exception)
            {
                throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse, exception);
            }

            if (response is null || !response.IsSuccess || response.Data is null)
            {
                throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse);
            }

            logger.LogInformation("Finance MCP tool {ToolName} completed successfully.", toolName);
            return response.Data;
        }, cancellationToken);
    }

    private async Task<McpClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (client is not null)
        {
            return client;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (client is not null)
            {
                return client;
            }

            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = CreateMcpEndpoint(options.Value.Finance.BaseUrl),
                    TransportMode = HttpTransportMode.StreamableHttp,
                    ConnectionTimeout = TimeSpan.FromSeconds(options.Value.Finance.TimeoutSeconds)
                },
                httpClient,
                ownsHttpClient: true);
            try
            {
                client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            }
            catch
            {
                await transport.DisposeAsync();
                throw;
            }

            logger.LogInformation("Connected to Finance MCP over Streamable HTTP.");
            return client;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<T> ExecuteDependencyOperationAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.Finance.TimeoutSeconds));
        try
        {
            return await operation(timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new McpDependencyException(DependencyName, McpDependencyFailureKind.Timeout, exception);
        }
        catch (McpDependencyException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new McpDependencyException(DependencyName, McpDependencyFailureKind.Unavailable, exception);
        }
    }

    private void EnsureEnabled()
    {
        if (!options.Value.Finance.Enabled)
        {
            throw new McpDependencyException(DependencyName, McpDependencyFailureKind.Disabled);
        }
    }

    internal static Uri CreateMcpEndpoint(string baseUrl) =>
        new($"{baseUrl.TrimEnd('/')}/mcp", UriKind.Absolute);

    private DateOnly GetCurrentDate() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static SalesSummary ToSalesSummary(McpSalesSummary summary) => new(
        ToSalesPeriod(summary.Period),
        summary.NetRevenue,
        summary.CostOfGoodsSold,
        summary.GrossProfit,
        summary.GrossMarginPercent,
        summary.QuantitySold,
        summary.OrderCount,
        summary.AverageOrderValue,
        summary.TopProduct is null ? null : ToTopProduct(summary.TopProduct),
        summary.Warnings);

    private static SalesPeriod ToSalesPeriod(McpSalesPeriod period) => new(
        DateOnly.ParseExact(period.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateOnly.ParseExact(period.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static TopProduct ToTopProduct(McpTopProduct product) => new(
        product.ProductCode,
        product.ProductName,
        product.QuantitySold,
        product.NetRevenue,
        product.GrossProfit);

    private static SalesChangeDirection ParseDirection(string direction) => direction.ToLowerInvariant() switch
    {
        "increased" => SalesChangeDirection.Increased,
        "decreased" => SalesChangeDirection.Decreased,
        "unchanged" => SalesChangeDirection.Unchanged,
        _ => throw new McpDependencyException(DependencyName, McpDependencyFailureKind.InvalidResponse)
    };

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (client is not null)
            {
                await client.DisposeAsync();
            }

            client = null;
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }

    private sealed record FinanceMcpResponse<T>(bool IsSuccess, T? Data, string? Error);
    private sealed record McpSalesPeriod(string StartDate, string EndDate);
    private sealed record McpTopProduct(string ProductCode, string ProductName, decimal QuantitySold, decimal NetRevenue, decimal GrossProfit);
    private sealed record McpSalesSummary(McpSalesPeriod Period, decimal NetRevenue, decimal CostOfGoodsSold, decimal GrossProfit, decimal GrossMarginPercent, decimal QuantitySold, int OrderCount, decimal AverageOrderValue, McpTopProduct? TopProduct, IReadOnlyList<string> Warnings);
    private sealed record McpPeriodComparison(McpSalesSummary CurrentPeriod, McpSalesSummary PreviousPeriod, decimal NetRevenueChange, decimal? NetRevenueChangePercentage, string Direction, IReadOnlyList<string> Warnings);
    private sealed record McpTopProducts(McpSalesPeriod Period, IReadOnlyList<McpTopProduct> Products, IReadOnlyList<string> Warnings);
    private sealed record McpYearlySalesTotal(int Year, decimal NetRevenue);
    private sealed record McpHistoricalSales(IReadOnlyList<McpYearlySalesTotal> Totals, IReadOnlyList<string> Warnings);
    private sealed record McpBudgetTarget(int Year, int? Month, bool IsAvailable, decimal? SalesTarget, decimal? ProfitTarget, string? AssumptionReference, IReadOnlyList<string> Warnings);
}
