using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sessions;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The snapshot's state history (collector spec CA-2): the last 20 state changes of each live session in this run.</summary>
public sealed class CollectHistoryTests
{
    private const string Id = "aaaaaaaa-0000-4000-8000-000000000010";

    private readonly CollectorHarness _c = new();

    private static SessionRef Ref => new(Core.Launch.AgentId.From("claude-code"), SessionId.From(Id));

    private async Task PassWith(string status)
    {
        _ = _c.Fake.With(Proc(10, 1, "claude", argv0: "claude", ticks: 100), cwd: "/w/api");
        _ = _c.Records.With(new ClaudeRecord(10, "100") { Session = SessionId.From(Id), Kind = "interactive", Cwd = "/w/api", Status = status, StatusUpdatedAt = _c.Now });
        _ = await _c.Pass();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pass_records_the_first_state_and_each_change_but_not_a_repeat()
    {
        await PassWith("busy");
        _c.Base.Clock.Advance(TimeSpan.FromSeconds(5));
        await PassWith("busy");
        _c.Base.Clock.Advance(TimeSpan.FromSeconds(5));
        await PassWith("idle");

        var history = _c.Snapshot.Current!.History[Ref];

        Assert.Equal([SessionState.Running, SessionState.Idle], history.Select(h => h.State));
        Assert.Equal(StateSource.Record, history[0].Source);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pass_keeps_only_the_last_20_changes()
    {
        for (var i = 0; i < 30; i++)
        {
            await PassWith(i % 2 == 0 ? "busy" : "idle");
            _c.Base.Clock.Advance(TimeSpan.FromSeconds(5));
        }

        var history = _c.Snapshot.Current!.History[Ref];

        Assert.Equal(20, history.Count);
        Assert.Equal(SessionState.Idle, history[^1].State);
        Assert.True(history[0].At < history[^1].At);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pass_forgets_the_history_of_a_session_that_left()
    {
        await PassWith("busy");
        _c.Fake.Exit(10);

        _ = await _c.After(TimeSpan.FromSeconds(5));

        Assert.Empty(_c.Snapshot.Current!.History);
    }
}
