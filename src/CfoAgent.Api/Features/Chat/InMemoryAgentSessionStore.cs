using System.Collections.Concurrent;
using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Features.Chat;

public sealed class InMemoryAgentSessionStore(
    IOptions<AgentSessionOptions> options,
    TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, SessionState> sessions = new(StringComparer.Ordinal);
    private readonly AgentSessionOptions options = options.Value;

    public AgentSessionContext GetContext(string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        if (!sessions.TryGetValue(conversationId, out var session))
        {
            return AgentSessionContext.Empty;
        }

        lock (session.SyncRoot)
        {
            var now = timeProvider.GetUtcNow();
            if (IsExpired(session, now))
            {
                sessions.TryRemove(new KeyValuePair<string, SessionState>(conversationId, session));
                return AgentSessionContext.Empty;
            }

            session.LastAccessed = now;
            return new AgentSessionContext(session.Turns.ToArray());
        }
    }

    public void Record(string conversationId, AgentResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(result);

        var now = timeProvider.GetUtcNow();
        var session = sessions.GetOrAdd(conversationId, _ =>
        {
            TrimToCapacity(now);
            return new SessionState(now);
        });

        lock (session.SyncRoot)
        {
            if (IsExpired(session, now))
            {
                session.Turns.Clear();
            }

            session.Turns.Add(new AgentSessionTurn(result.ResponseType, result.DataPeriod));
            if (session.Turns.Count > options.MessageLimit)
            {
                session.Turns.RemoveRange(0, session.Turns.Count - options.MessageLimit);
            }

            session.LastAccessed = now;
        }
    }

    private bool IsExpired(SessionState session, DateTimeOffset now) =>
        now - session.LastAccessed >= TimeSpan.FromMinutes(options.ExpirationMinutes);

    private void TrimToCapacity(DateTimeOffset now)
    {
        foreach (var pair in sessions)
        {
            if (IsExpired(pair.Value, now))
            {
                sessions.TryRemove(pair);
            }
        }

        if (sessions.Count < options.MaximumSessions)
        {
            return;
        }

        var oldest = sessions.OrderBy(pair => pair.Value.LastAccessed).FirstOrDefault();
        if (!string.IsNullOrEmpty(oldest.Key))
        {
            sessions.TryRemove(oldest);
        }
    }

    private sealed class SessionState(DateTimeOffset lastAccessed)
    {
        public object SyncRoot { get; } = new();

        public List<AgentSessionTurn> Turns { get; } = [];

        public DateTimeOffset LastAccessed { get; set; } = lastAccessed;
    }
}
