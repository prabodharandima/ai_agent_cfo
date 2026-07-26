using CfoAgent.Api.Agents.Contracts;
using CfoAgent.Api.Configuration;
using CfoAgent.Api.Features.Chat;
using Microsoft.Extensions.Options;

namespace CfoAgent.Api.Tests.Features.Chat;

public sealed class InMemoryAgentSessionStoreTests
{
    [Fact]
    public void SameSession_RetainsOnlyPriorResolvedTurnMetadata()
    {
        var store = CreateStore();

        store.Record("session-a", CreateResult(AgentResponseType.SalesSummary, "sensitive answer and raw retrieved content"));

        var context = store.GetContext("session-a");

        var turn = Assert.Single(context.Turns);
        Assert.Equal(AgentResponseType.SalesSummary, turn.ResponseType);
        Assert.Equal("Current week", turn.DataPeriod?.Label);
        Assert.DoesNotContain("sensitive", string.Join(' ', context.Turns.Select(item => item.ToString())));
        Assert.DoesNotContain("retrieved", string.Join(' ', context.Turns.Select(item => item.ToString())));
    }

    [Fact]
    public void DifferentSessions_AreIsolated()
    {
        var store = CreateStore();
        store.Record("session-a", CreateResult(AgentResponseType.SalesSummary));
        store.Record("session-b", CreateResult(AgentResponseType.Forecast));

        Assert.Equal(AgentResponseType.SalesSummary, Assert.Single(store.GetContext("session-a").Turns).ResponseType);
        Assert.Equal(AgentResponseType.Forecast, Assert.Single(store.GetContext("session-b").Turns).ResponseType);
    }

    [Fact]
    public void ExpiredSession_IsRemoved()
    {
        var timeProvider = new MutableTimeProvider();
        var store = CreateStore(timeProvider, expirationMinutes: 5);
        store.Record("session-a", CreateResult(AgentResponseType.Knowledge));

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        Assert.Empty(store.GetContext("session-a").Turns);
    }

    [Fact]
    public void MessageLimit_KeepsTheMostRecentTurns()
    {
        var store = CreateStore(messageLimit: 2);
        store.Record("session-a", CreateResult(AgentResponseType.SalesSummary));
        store.Record("session-a", CreateResult(AgentResponseType.Forecast));
        store.Record("session-a", CreateResult(AgentResponseType.Knowledge));

        Assert.Equal(
            [AgentResponseType.Forecast, AgentResponseType.Knowledge],
            store.GetContext("session-a").Turns.Select(turn => turn.ResponseType));
    }

    private static InMemoryAgentSessionStore CreateStore(
        MutableTimeProvider? timeProvider = null,
        int messageLimit = 8,
        int expirationMinutes = 30) =>
        new(
            Options.Create(new AgentSessionOptions
            {
                MessageLimit = messageLimit,
                ExpirationMinutes = expirationMinutes,
                MaximumSessions = 10
            }),
            timeProvider ?? new MutableTimeProvider());

    private static AgentResult CreateResult(AgentResponseType responseType, string answer = "verified answer") =>
        new(
            answer,
            responseType,
            Array.Empty<string>(),
            null,
            Array.Empty<AgentSource>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            new AgentDataPeriod(new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 19), "Current week"));

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset now = new(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan value) => now = now.Add(value);
    }
}
