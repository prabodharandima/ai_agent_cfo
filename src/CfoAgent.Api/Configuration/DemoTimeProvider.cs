using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Configuration;

public sealed class DemoTimeProvider : TimeProvider
{
    private readonly DateTimeOffset utcNow;

    public DemoTimeProvider(IOptions<FinanceOptions> financeOptions)
    {
        var demoDate = financeOptions.Value.DemoDate;
        utcNow = new DateTimeOffset(demoDate.Year, demoDate.Month, demoDate.Day, 0, 0, 0, TimeSpan.Zero);
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => utcNow;
}
