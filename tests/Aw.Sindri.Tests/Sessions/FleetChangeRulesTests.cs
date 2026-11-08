using Aw.Sindri.Core.Sessions;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Which changes reach the ledger (collector spec CO3).</summary>
public sealed class FleetChangeRulesTests
{
    private static FleetDiff Pass(FleetTrackerState state, DateTimeOffset now, bool good = true, params ObservedSession[] sessions) =>
        FleetChangeRules.Diff(state, sessions, now, good);

    [Fact, Trait("Lane", "unit")]
    public void Changes_ledger_appeared_after_two_passes()
    {
        var s = Session("claude-code", "a1");

        var first = Pass(FleetTrackerState.Empty, T0, sessions: s);
        var second = Pass(first.State, T0.AddSeconds(2), sessions: s);
        var third = Pass(second.State, T0.AddSeconds(4), sessions: s);

        Assert.Empty(first.Changes);
        var change = Assert.Single(second.Changes);
        Assert.Equal((FleetChangeKind.Appeared, "claude-code/a1", T0.AddSeconds(2)), (change.Kind, change.Ref.ToString(), change.ObservedAt));
        Assert.Empty(third.Changes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Gone_carries_the_repo_host_and_launch_the_session_appeared_with()
    {
        var s = Session("claude-code", "a1") with { Repo = Core.Ids.RepoName.From("api"), Host = new HostInfo("tmux", "tmux"), LaunchId = Core.Launch.LaunchId.From("LCH-00000000000000000000000000000001") };
        var seen = Pass(Pass(FleetTrackerState.Empty, T0, sessions: s).State, T0.AddSeconds(2), sessions: s);

        var gone = Assert.Single(Pass(seen.State, T0.AddSeconds(4)).Changes);

        Assert.Equal((FleetChangeKind.Gone, "api", "tmux", "LCH-00000000000000000000000000000001"), (gone.Kind, gone.Repo!.Value.Value, gone.HostKind, gone.LaunchId!.Value.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void Gone_after_a_restart_still_carries_the_repo_of_the_restored_row()
    {
        var row = new FleetHistoryRow(Session("claude-code", "a1").Ref, FleetChangeKind.Appeared, T0) { Repo = Core.Ids.RepoName.From("api"), HostKind = "tmux" };
        var restored = FleetChangeRules.Restore([row]);

        var gone = Assert.Single(Pass(restored, T0.AddSeconds(4)).Changes);

        Assert.Equal((FleetChangeKind.Gone, "api", "tmux"), (gone.Kind, gone.Repo!.Value.Value, gone.HostKind));
    }

    [Fact, Trait("Lane", "unit")]
    public void Changes_do_not_ledger_a_session_seen_for_one_pass()
    {
        var s = Session("claude-code", "a1");
        var first = Pass(FleetTrackerState.Empty, T0, sessions: s);
        var gap = Pass(first.State, T0.AddSeconds(2));
        var again = Pass(gap.State, T0.AddSeconds(4), sessions: s);

        Assert.All([first, gap, again], d => Assert.Empty(d.Changes));
    }

    [Fact, Trait("Lane", "unit")]
    public void Changes_ledger_gone_once_when_a_good_pass_lacks_a_reported_session()
    {
        var s = Session("claude-code", "a1");
        var state = Pass(Pass(FleetTrackerState.Empty, T0, sessions: s).State, T0.AddSeconds(2), sessions: s).State;

        var gone = Pass(state, T0.AddSeconds(4));
        var after = Pass(gone.State, T0.AddSeconds(6));

        Assert.Equal([FleetChangeKind.Gone], gone.Changes.Select(c => c.Kind));
        Assert.Empty(after.Changes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Changes_no_gone_when_the_process_table_fails()
    {
        var s = Session("claude-code", "a1");
        var state = Pass(Pass(FleetTrackerState.Empty, T0, sessions: s).State, T0.AddSeconds(2), sessions: s).State;

        var failed = Pass(state, T0.AddSeconds(4), good: false);

        Assert.Empty(failed.Changes);
        Assert.Same(state, failed.State);
        Assert.Equal([FleetChangeKind.Gone], Pass(failed.State, T0.AddSeconds(6)).Changes.Select(c => c.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public void Changes_ledger_asking_after_5s_and_at_most_6_an_hour()
    {
        var state = FleetTrackerState.Empty;
        var kinds = new List<FleetChangeKind>();
        var now = T0;
        state = Pass(state, now, sessions: Session("claude-code", "a1")).State;
        state = Pass(state, now.AddSeconds(2), sessions: Session("claude-code", "a1")).State;
        for (var episode = 0; episode < 8; episode++)
        {
            var since = now.AddMinutes(episode * 5);
            foreach (var offset in new[] { 0, 3, 6, 9 })   // asking for 0, 3 (under 5 s), 6 and 9 seconds
            {
                var d = Pass(state, since.AddSeconds(offset), sessions: Session("claude-code", "a1", SessionState.Asking, since));
                state = d.State;
                kinds.AddRange(d.Changes.Select(c => c.Kind));
            }

            state = Pass(state, since.AddSeconds(20), sessions: Session("claude-code", "a1", SessionState.Running, since.AddSeconds(15))).State;
        }

        // eight episodes inside 40 minutes: 6 in the first hour are ledgered, then the cap holds; none repeats within an episode
        Assert.Equal(6, kinds.Count(k => k == FleetChangeKind.Asking));
    }

    [Fact, Trait("Lane", "unit")]
    public void Changes_ask_not_before_5_seconds()
    {
        var state = Pass(Pass(FleetTrackerState.Empty, T0, sessions: Session("claude-code", "a1")).State, T0.AddSeconds(2), sessions: Session("claude-code", "a1")).State;
        var asking = Session("claude-code", "a1", SessionState.Asking, T0.AddSeconds(2));

        var early = Pass(state, T0.AddSeconds(6), sessions: asking);
        var due = Pass(early.State, T0.AddSeconds(7), sessions: asking);

        Assert.Empty(early.Changes);
        Assert.Equal(FleetChangeKind.Asking, Assert.Single(due.Changes).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Changes_follow_a_session_whose_ref_changed_but_whose_process_did_not()
    {
        var anonymous = Session("claude-code", "pid-10-100");
        var named = Session("claude-code", "real-id");
        var state = Pass(Pass(FleetTrackerState.Empty, T0, sessions: anonymous).State, T0.AddSeconds(2), sessions: anonymous).State;

        var renamed = Pass(state, T0.AddSeconds(4), sessions: named);

        Assert.Empty(renamed.Changes);
        Assert.Contains(named.Ref, renamed.State.Reported.Keys);
    }

    [Fact, Trait("Lane", "unit")]
    public void Restart_rebuilds_the_reported_set_from_the_ledger()
    {
        var a = Session("claude-code", "a1");
        var b = Session("claude-code", "b2");
        FleetHistoryRow[] history =
        [
            new(a.Ref, FleetChangeKind.Appeared, T0), new(b.Ref, FleetChangeKind.Appeared, T0), new(b.Ref, FleetChangeKind.Gone, T0.AddHours(1)),
        ];

        var restored = FleetChangeRules.Restore(history);
        var firstPass = Pass(restored, T0.AddHours(2), sessions: a);
        var gone = Pass(restored, T0.AddHours(2), sessions: []);

        Assert.Equal([a.Ref], restored.Reported.Keys);
        Assert.Empty(firstPass.Changes);   // a is still live and reported: no second 'appeared'
        Assert.Equal([a.Ref.ToString()], gone.Changes.Select(c => c.Ref.ToString()), StringComparer.Ordinal);
        Assert.All(gone.Changes, c => Assert.Equal(FleetChangeKind.Gone, c.Kind));
    }
}
