using System.Globalization;
using System.Text.RegularExpressions;
using CfoAgent.Api.Features.Sales;

namespace CfoAgent.Api.Agents;

internal static class SalesSummaryPeriodResolver
{
    private static readonly Regex DayMonthPattern = new(
        @"\b(?<day>\d{1,2})(?:st|nd|rd|th)?\s+(?:of\s+)?(?<month>january|february|march|april|may|june|july|august|september|october|november|december)\s+(?:(?<thisYear>this\s+year)|(?<year>\d{4}))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ResolvedSalesSummaryPeriod Resolve(string message, DateOnly currentDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (TryResolveExplicitDate(message, currentDate.Year, out var date))
        {
            return new ResolvedSalesSummaryPeriod(new SalesPeriod(date, date), "Selected date");
        }

        if (ContainsWord(message, "yesterday"))
        {
            var yesterday = currentDate.AddDays(-1);
            return new ResolvedSalesSummaryPeriod(new SalesPeriod(yesterday, yesterday), "Yesterday");
        }

        if (ContainsWord(message, "today"))
        {
            return new ResolvedSalesSummaryPeriod(new SalesPeriod(currentDate, currentDate), "Today");
        }

        return new ResolvedSalesSummaryPeriod(
            new SalesPeriod(StartOfWeek(currentDate), currentDate),
            "Current week");
    }

    private static bool TryResolveExplicitDate(string message, int currentYear, out DateOnly date)
    {
        var match = DayMonthPattern.Match(message);
        if (!match.Success ||
            !int.TryParse(match.Groups["day"].Value, CultureInfo.InvariantCulture, out var day) ||
            !DateTime.TryParseExact(
                match.Groups["month"].Value,
                "MMMM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedMonth))
        {
            date = default;
            return false;
        }

        var year = match.Groups["thisYear"].Success
            ? currentYear
            : int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);

        try
        {
            date = new DateOnly(year, parsedMonth.Month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            date = default;
            return false;
        }
    }

    private static bool ContainsWord(string message, string word) =>
        Regex.IsMatch(message, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);
}

internal sealed record ResolvedSalesSummaryPeriod(SalesPeriod Period, string Label);
