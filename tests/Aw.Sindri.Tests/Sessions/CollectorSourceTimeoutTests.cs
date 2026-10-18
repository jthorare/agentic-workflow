using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>
/// Per-source timeouts in a pass (collector spec §4.1): who stays live, what clears, what is never started twice. Time is the harness's fake clock and a read is
/// "late" only when a test advances it after the read has started, so nothing here depends on how busy the machine is.
/// </summary>
public sealed class CollectorSourceTimeoutTests
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _environmentShare = TimeSpan.FromMilliseconds(250);

    private static Dictionary<string, string> Env(params (string, string)[] pairs) => pairs.ToDictionary(static p => p.Item1, static p => p.Item2, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public async Task One_stuck_pids_environment_leaves_the_other_pids_and_a_stuck_judge_child_is_not_published()
    {
        var fake = new FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        _ = fake.With(Proc(10, 1, "claude", argv0: "claude"), cwd: "/w/a").With(Proc(11, 1, "claude", argv0: "claude", ticks: 101), cwd: "/w/b")
            .With(Proc(12, 1, "claude", argv0: "claude", ticks: 102), Env(("AW_JUDGE_CHILD", "1")));
        _ = await c.Pass();
        _ = await c.After(TimeSpan.FromSeconds(2));
        var pendingBefore = c.Memory.Pending.Count;

        table.StickEnvironment(10);
        table.StickEnvironment(12);
        c.Base.Clock.Advance(TimeSpan.FromSeconds(2));
        var pass = c.Pass(_budget);
        await table.Environments.Reached(1);
        c.Base.Clock.Advance(_environmentShare);
        await table.Environments.Reached(2);
        c.Base.Clock.Advance(_environmentShare);
        var response = await pass;
        table.Release();

        Assert.True(response.Good);
        Assert.Equal("claude-code/pid-10-100,claude-code/pid-11-101", string.Join(',', c.Snapshot.Current!.Sessions.Select(static s => s.Ref.ToString())));
        Assert.Equal(pendingBefore, c.Memory.Pending.Count);   // no 'gone' for 10, no 'appeared' for 12
        var problem = Assert.Single(c.Snapshot.Current.Problems);
        Assert.Equal(("environment", "SND-SESSION-001"), (problem.Source, problem.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_backed_session_whose_environment_goes_unreadable_keeps_its_lane_and_launch()
    {
        var fake = new FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        var id = "aaaaaaaa-0000-4000-8000-000000000010";
        _ = fake.With(Proc(10, 1, "claude", argv0: "claude"), Env(("AW_META_LANE", "lane-a"), ("AW_META_TEAM", "t1")), cwd: "/w/api");
        _ = c.Records.With(new ClaudeRecord(10, "100") { Session = SessionId.From(id), Kind = "interactive", Cwd = "/w/api", Name = "api", Status = "busy", StatusUpdatedAt = c.Now });
        _ = await c.Pass();
        Assert.Equal("lane-a", Assert.Single(c.Snapshot.Current!.Sessions).Lane);

        table.StickEnvironment(10);
        var pass = c.Pass(_budget);
        await table.Environments.Reached(1);
        c.Base.Clock.Advance(_environmentShare);
        _ = await pass;
        table.Release();

        var kept = Assert.Single(c.Snapshot.Current.Sessions);
        Assert.Equal(("lane-a", "t1"), (kept.Lane, kept.Team));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_never_seen_record_backed_session_with_an_unreadable_environment_is_not_published_yet()
    {
        var fake = new FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        _ = fake.With(Proc(10, 1, "claude", argv0: "claude"), cwd: "/w/api");
        _ = c.Records.With(new ClaudeRecord(10, "100") { Session = SessionId.From("aaaaaaaa-0000-4000-8000-000000000010"), Kind = "interactive", Cwd = "/w/api", Status = "busy", StatusUpdatedAt = c.Now });
        table.StickEnvironment(10);

        var pass = c.Pass(_budget);
        await table.Environments.Reached(1);
        c.Base.Clock.Advance(_environmentShare);
        _ = await pass;
        table.Release();

        Assert.Empty(c.Snapshot.Current!.Sessions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reused_pid_does_not_inherit_the_old_sessions_state()
    {
        var fake = new FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        _ = fake.With(Proc(10, 1, "claude", argv0: "claude", ticks: 100), cwd: "/w/a");
        _ = await c.Pass();
        _ = await c.After(TimeSpan.FromSeconds(2));
        fake.Exit(10);
        _ = fake.With(Proc(10, 1, "claude", argv0: "claude", ticks: 999), cwd: "/w/b");
        table.StickEnvironment(10);

        var pass = c.Pass(_budget);
        await table.Environments.Reached(1);
        c.Base.Clock.Advance(_environmentShare);
        _ = await pass;
        table.Release();

        Assert.Empty(c.Snapshot.Current!.Sessions);   // the old one is gone; the new process is unknown and unseen
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Abandoned_reads_of_processes_that_are_gone_are_forgotten()
    {
        var fake = new FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        var budget = TimeSpan.FromHours(500);
        var pids = Enumerable.Range(10, 5).ToList();   // a source gets five shares of the budget: five stuck pids is what one pass can abandon
        foreach (var pid in pids)
        {
            _ = fake.With(Proc(pid, 1, "claude", argv0: "claude", ticks: pid), cwd: "/w/a");
            table.StickEnvironment(pid);
        }

        var pass = c.Pass(budget);
        for (var n = 1; n <= pids.Count; n++)
        {
            await table.Environments.Reached(n);
            c.Base.Clock.Advance(budget / 5);
        }

        _ = await pass;
        Assert.Equal(pids.Count, c.Discovery.AbandonedCount);
        foreach (var pid in pids)
        {
            fake.Exit(pid);
        }

        table.Release();
        _ = await c.Pass(budget);

        Assert.Equal(0, c.Discovery.AbandonedCount);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_released_source_is_read_again_and_its_problem_row_clears()
    {
        using var blocked = new BlockingSources();
        var c = new CollectorHarness(records: blocked, fleet: blocked);
        _ = c.Fake.With(Proc(10, 1, "claude", argv0: "claude"), cwd: "/w/api");
        var first = c.Pass(_budget);
        await blocked.Records.Reached(1);
        c.Base.Clock.Advance(_budget);
        await blocked.Fleet.Reached(1);
        c.Base.Clock.Advance(_budget);
        _ = await first;
        Assert.Equal(2, c.Snapshot.Current!.Problems.Count);

        blocked.Release();
        await c.Discovery.AbandonedSettled();   // the released reads have returned
        _ = await c.Pass(_budget);

        Assert.Empty(c.Snapshot.Current.Problems);
        Assert.Equal((2, 2), (blocked.RecordReads, blocked.FleetReads));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_given_up_on_has_its_read_cancelled()
    {
        using var blocked = new BlockingSources();
        var c = new CollectorHarness(records: blocked, fleet: blocked);

        var pass = c.Pass(_budget);
        await blocked.Records.Reached(1);
        c.Base.Clock.Advance(_budget);
        await blocked.Fleet.Reached(1);
        c.Base.Clock.Advance(_budget);
        _ = await pass;
        var cancelled = blocked.RecordToken.IsCancellationRequested;
        blocked.Release();

        Assert.True(cancelled);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_process_table_that_overruns_twice_keeps_the_previous_snapshot_and_is_not_read_again()
    {
        var fake = new FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        _ = fake.With(Proc(10, 1, "claude", argv0: "claude"), cwd: "/w/api");
        _ = await c.Pass();
        table.BlockReads = true;

        var firstPass = c.Pass(_budget);
        await table.Tables.Reached(2);
        c.Base.Clock.Advance(_budget);
        var first = await firstPass;
        var second = await c.Pass(_budget);   // still blocked: not started again, so there is nothing to wait for
        table.Release();

        Assert.Equal((true, true), (first.Overran, second.Overran));
        Assert.Equal(2, table.Reads);   // the good pass and the first overrun
        _ = Assert.Single(c.Snapshot.Current!.Sessions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_full_pending_queue_drops_the_oldest_and_logs_once_then_again_after_it_drains_and_overflows_again()
    {
        var log = new ListLogger<CollectSessionsService>();
        var c = new CollectorHarness(logger: log);
        var old = new FleetChange(new SessionRef(AgentId.From("claude-code"), SessionId.From("aaaaaaaa-0000-4000-8000-0000000000ff")), FleetChangeKind.Gone, SessionState.Running, c.Now);
        c.Memory.Pending = [.. Enumerable.Repeat(old, CollectorMemory.PendingCap)];
        _ = c.Fake.With(Proc(10, 1, "claude", argv0: "claude"), cwd: "/w/api");

        _ = await c.Pass();
        _ = await c.After(TimeSpan.FromSeconds(2));   // the appeared change lands: over the cap
        _ = await c.After(TimeSpan.FromSeconds(2));
        Assert.Equal(CollectorMemory.PendingCap, c.Memory.Pending.Count);
        Assert.Equal(FleetChangeKind.Appeared, c.Memory.Pending[^1].Kind);
        Assert.Equal(1, Overflows(log));

        c.Memory.Pending = [];
        _ = await c.After(TimeSpan.FromSeconds(2));   // below the cap: the next overflow is news again
        c.Memory.Pending = [.. Enumerable.Repeat(old, CollectorMemory.PendingCap)];
        _ = c.Fake.With(Proc(11, 1, "claude", argv0: "claude", ticks: 101), cwd: "/w/web");
        _ = await c.After(TimeSpan.FromSeconds(2));
        _ = await c.After(TimeSpan.FromSeconds(2));

        Assert.Equal(2, Overflows(log));
    }

    private static int Overflows(ListLogger<CollectSessionsService> log) => log.Lines.Count(static l => l.Contains("fleet changes the ledger hasn't taken", StringComparison.Ordinal));
}
