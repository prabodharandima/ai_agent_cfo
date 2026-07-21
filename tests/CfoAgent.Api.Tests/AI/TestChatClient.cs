using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace CfoAgent.Api.Tests.AI;

internal sealed class TestChatClient : IChatClient
{
    private readonly Func<string, ChatOptions?, CancellationToken, Task<string>> responseFactory;

    public TestChatClient(
        Func<string, ChatOptions?, CancellationToken, Task<string>>? responseFactory = null,
        string providerName = "Test",
        string modelId = "test-model")
    {
        this.responseFactory = responseFactory ?? GetMvpResponseAsync;
        Metadata = new ChatClientMetadata(providerName, new Uri("https://test.local"), modelId);
    }

    public ChatClientMetadata Metadata { get; }

    public static TestChatClient CreateMvp(TimeSpan? delay = null, Exception? exception = null) =>
        new(async (prompt, options, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (exception is not null)
            {
                return await Task.FromException<string>(exception);
            }

            if (delay is not null)
            {
                await Task.Delay(delay.Value, cancellationToken);
            }

            return await GetMvpResponseAsync(prompt, options, cancellationToken);
        });

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = string.Join('\n', messages.Select(message => message.Text ?? string.Empty));
        var response = await responseFactory(prompt, options, cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, response))
        {
            ModelId = options?.ModelId ?? Metadata.DefaultModelId
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text)
        {
            ModelId = response.ModelId
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata) ? Metadata
        : serviceType.IsInstanceOfType(this) ? this
        : null;

    public void Dispose()
    {
    }

    private static Task<string> GetMvpResponseAsync(string prompt, ChatOptions? _, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (prompt.Contains("USER_REQUEST:", StringComparison.Ordinal))
        {
            return Task.FromResult(Classify(prompt));
        }

        if (prompt.Contains("SALES_SUMMARY_PERIOD_REQUEST:", StringComparison.Ordinal))
        {
            return Task.FromResult(ResolveSalesSummaryDateRange(prompt));
        }

        if (prompt.Contains("VERIFIED_DATA:", StringComparison.Ordinal))
        {
            return Task.FromResult($"Verified test response:\n{GetContentAfter(prompt, "VERIFIED_DATA:")}");
        }

        if (prompt.Contains("RETRIEVED_CONTEXT:", StringComparison.Ordinal))
        {
            return Task.FromResult($"Verified test response:\n{GetContentAfter(prompt, "RETRIEVED_CONTEXT:")}");
        }

        return Task.FromResult("Verified test response.");
    }

    private static string Classify(string prompt)
    {
        var normalized = GetContentAfter(prompt, "USER_REQUEST:").ToUpperInvariant();
        var hasForecast = normalized.Contains("FORECAST", StringComparison.Ordinal);
        var hasKnowledge = normalized.Contains("TARGET", StringComparison.Ordinal)
            || normalized.Contains("ASSUMPTION", StringComparison.Ordinal)
            || normalized.Contains("RISK", StringComparison.Ordinal);

        if (hasForecast && hasKnowledge)
        {
            return "Mixed";
        }

        if (hasForecast)
        {
            return "Forecast";
        }

        if (normalized.Contains("COMPARE", StringComparison.Ordinal) || normalized.Contains("VERSUS", StringComparison.Ordinal))
        {
            return "SalesComparison";
        }

        if (normalized.Contains("TOP", StringComparison.Ordinal) && normalized.Contains("PRODUCT", StringComparison.Ordinal))
        {
            return "TopProducts";
        }

        if (hasKnowledge)
        {
            return "Knowledge";
        }

        if (normalized.Contains("SALES", StringComparison.Ordinal) || normalized.Contains("WEEK", StringComparison.Ordinal))
        {
            return "SalesSummary";
        }

        return "Unsupported";
    }

    private static string ResolveSalesSummaryDateRange(string prompt)
    {
        const string currentDateMarker = "The current date is ";
        var markerIndex = prompt.IndexOf(currentDateMarker, StringComparison.Ordinal);
        var currentDate = DateOnly.ParseExact(
            prompt.Substring(markerIndex + currentDateMarker.Length, 10),
            "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture);
        var message = GetContentAfter(prompt, "SALES_SUMMARY_PERIOD_REQUEST:").ToUpperInvariant();

        var (startDate, endDate) = message.Contains("LAST WEEK", StringComparison.Ordinal)
            ? (StartOfWeek(currentDate).AddDays(-7), StartOfWeek(currentDate).AddDays(-1))
            : message.Contains("SINCE YESTERDAY", StringComparison.Ordinal)
                ? (currentDate.AddDays(-1), currentDate)
                : message.Contains("YESTERDAY", StringComparison.Ordinal)
                    ? (currentDate.AddDays(-1), currentDate.AddDays(-1))
                    : message.Contains("TODAY", StringComparison.Ordinal)
                        ? (currentDate, currentDate)
                        : (StartOfWeek(currentDate), currentDate);

        return JsonSerializer.Serialize(new
        {
            startDate = startDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            endDate = endDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
        });
    }

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);

    private static string GetContentAfter(string value, string marker)
    {
        var markerIndex = value.IndexOf(marker, StringComparison.Ordinal);
        return markerIndex < 0 ? string.Empty : value[(markerIndex + marker.Length)..].Trim();
    }
}
