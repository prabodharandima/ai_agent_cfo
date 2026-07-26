using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CfoAgent.Api.Observability;

public static class AgentActivityTracing
{
    public const string ActivitySourceName = "CfoAgent.Api.Agent";
    public const string MeterName = "CfoAgent.Api.Agent";

    private static readonly ActivitySource Source = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("cfo.agent.operation.duration", "ms");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("cfo.agent.operation.failures");
    private static readonly Counter<long> Cancellations = Meter.CreateCounter<long>("cfo.agent.operation.cancellations");

    public static Activity? Start(string operation, string? agent = null, string? provider = null, string? model = null) =>
        Source.StartActivity(operation, ActivityKind.Internal) is { } activity
            ? SetSafeTags(activity, operation, agent, provider, model)
            : null;

    public static void Complete(
        Activity? activity,
        string operation,
        Stopwatch stopwatch,
        string outcome,
        string? agent = null,
        string? provider = null,
        string? model = null)
    {
        var duration = stopwatch.Elapsed.TotalMilliseconds;
        activity?.SetTag("cfo.outcome", outcome);
        activity?.SetTag("cfo.duration.ms", duration);
        if (string.Equals(outcome, "Failure", StringComparison.Ordinal))
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            Failures.Add(1, Tags(operation, agent, provider, model, outcome));
        }
        else if (string.Equals(outcome, "Cancelled", StringComparison.Ordinal))
        {
            Cancellations.Add(1, Tags(operation, agent, provider, model, outcome));
        }

        Duration.Record(duration, Tags(operation, agent, provider, model, outcome));
        activity?.Dispose();
    }

    private static Activity SetSafeTags(Activity activity, string operation, string? agent, string? provider, string? model)
    {
        activity.SetTag("cfo.correlation_id", activity.TraceId.ToString());
        activity.SetTag("cfo.operation", operation);
        SetIfPresent(activity, "cfo.agent", agent);
        SetIfPresent(activity, "gen_ai.provider.name", provider);
        SetIfPresent(activity, "gen_ai.request.model", model);
        return activity;
    }

    private static TagList Tags(string operation, string? agent, string? provider, string? model, string outcome)
    {
        var tags = new TagList { { "cfo.operation", operation }, { "cfo.outcome", outcome } };
        if (!string.IsNullOrWhiteSpace(agent)) tags.Add("cfo.agent", agent);
        if (!string.IsNullOrWhiteSpace(provider)) tags.Add("gen_ai.provider.name", provider);
        if (!string.IsNullOrWhiteSpace(model)) tags.Add("gen_ai.request.model", model);
        return tags;
    }

    private static void SetIfPresent(Activity activity, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) activity.SetTag(key, value);
    }
}
