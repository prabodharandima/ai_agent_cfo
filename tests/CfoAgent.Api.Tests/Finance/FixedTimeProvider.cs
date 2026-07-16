namespace CfoAgent.Api.Tests.Finance;

internal sealed class FixedTimeProvider(DateOnly date) : TimeProvider
{
    private readonly DateTimeOffset now = new(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => now;
}
