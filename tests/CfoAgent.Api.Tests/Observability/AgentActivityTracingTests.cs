using System.Diagnostics;
using System.Collections.Concurrent;
using CfoAgent.Api.Observability;

namespace CfoAgent.Api.Tests.Observability;

public sealed class AgentActivityTracingTests
{
    [Fact]
    public void Complete_RecordsOnlySafeSuccessAttributes()
    {
        var capture = Listen();
        using var listener = capture.Listener;
        var stopped = capture.Stopped;
        var stopwatch = Stopwatch.StartNew();
        var activity = AgentActivityTracing.Start("llm.request", provider: "Ollama", model: "llama3.2:3b");

        AgentActivityTracing.Complete(activity, "llm.request", stopwatch, "Success", provider: "Ollama", model: "llama3.2:3b");

        var span = Assert.Single(stopped.Where(activity => activity.OperationName == "llm.request").ToArray());
        Assert.Equal("llm.request", span.OperationName);
        Assert.Equal("Success", span.GetTagItem("cfo.outcome"));
        Assert.Equal("Ollama", span.GetTagItem("gen_ai.provider.name"));
        Assert.Equal("llama3.2:3b", span.GetTagItem("gen_ai.request.model"));
        Assert.DoesNotContain(span.Tags, tag => tag.Key.Contains("prompt", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("response", StringComparison.OrdinalIgnoreCase)
            || tag.Key.Contains("content", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Complete_RecordsFailureWithoutExceptionContent()
    {
        var capture = Listen();
        using var listener = capture.Listener;
        var stopped = capture.Stopped;
        var activity = AgentActivityTracing.Start("finance-mcp.operation");

        AgentActivityTracing.Complete(activity, "finance-mcp.operation", Stopwatch.StartNew(), "Failure");

        var span = Assert.Single(stopped.Where(activity => activity.OperationName == "finance-mcp.operation").ToArray());
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("Failure", span.GetTagItem("cfo.outcome"));
        Assert.Empty(span.Events);
    }

    [Fact]
    public void Complete_RecordsCancellationAsDistinctOutcome()
    {
        var capture = Listen();
        using var listener = capture.Listener;
        var stopped = capture.Stopped;
        var activity = AgentActivityTracing.Start("chromadb.retrieval", agent: "FinancialKnowledgeAgent");

        AgentActivityTracing.Complete(activity, "chromadb.retrieval", Stopwatch.StartNew(), "Cancelled", "FinancialKnowledgeAgent");

        var span = Assert.Single(stopped.Where(activity => activity.OperationName == "chromadb.retrieval").ToArray());
        Assert.Equal("Cancelled", span.GetTagItem("cfo.outcome"));
        Assert.Equal("FinancialKnowledgeAgent", span.GetTagItem("cfo.agent"));
    }

    private static (ActivityListener Listener, ConcurrentQueue<Activity> Stopped) Listen()
    {
        var stopped = new ConcurrentQueue<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentActivityTracing.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(listener);
        return (listener, stopped);
    }
}
