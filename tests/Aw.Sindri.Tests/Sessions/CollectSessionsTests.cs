using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Testing;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>One collector pass (collector spec §4.1, §12): sources, state, problems, the previous snapshot kept when the table fails.</summary>
public sealed class CollectSessionsTests
{
    private const string Id = "aaaaaaaa-0000-4000-8000-000000000010";

    private readonly CollectorHarness _c = new();

    private static Dictionary<string, string> Env(params (string, string)[] pairs) => pairs.ToDictionary(static p => p.Item1, static p => p.Item2, StringComparer.Ordinal);

    private ClaudeRecord LiveRecord(string status = "busy") =>
        new(10, "100") { Session = SessionId.From(Id), Kind = "interactive", Cwd = "/w/api", Name = "api work", Status = status, StatusUpdatedAt = _c.Now };

    private void ClaudeRuns(int pid = 10, long ticks = 100) => _c.Fake.With(Proc(pid, 1, "claude", argv0: "claude", ticks: ticks), cwd: "/w/api");

    [Fact, Trait("Lane", "unit")]
    public async Task Pass_lists_a_claude_session_from_its_record_with_the_state_the_record_gives()
    {
        ClaudeRuns();
        _ = _c.Records.With(LiveRecord());

        var response = await _c.Pass();

        var session = Assert.Single(_c.Snapshot.Current!.Sessions);
        Assert.True(response.Good);
        Assert.Equal(($"claude-code/{Id}", SessionState.Running, StateSource.Record, true, "api work", "/w/api"), (session.Ref.ToString(), session.State, session.Source, session.IdKnown, session.Title, session.Cwd));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pass_turns_a_throwing_source_into_one_problem_row_and_keeps_the_other_sources()
    {
        ClaudeRuns();
        _ = _c.Records.With(LiveRecord());
        var guarded = new SessionDiscovery(new ThrowingEnvironmentTable(_c.Fake), _c.Records, _c.Fleet, new Infrastructure.Launch.AgentCatalog(Agents), _c.Settings, SessionHostData.None(_c.Fake), _c.Base.Clock);

        var found = await Task.Run(() => guarded.Discover(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);

        Assert.True(found!.Good);
        Assert.Equal("claude-code/" + Id, Assert.Single(found.Candidates).Ref.ToString());
        var problem = Assert.Single(found.Problems);
        Assert.Equal(("environment", "SND-SESSION-001"), (problem.Source, problem.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cancelled_process_table_read_is_a_failed_source_not_SND_SESSION_002()
    {
        var problem = SessionProblems.ProcessTable(Core.SindriErrors.ApiTimeout.With("the process table read was cancelled"));

        Assert.Equal(("processes", "SND-SESSION-001"), (problem.Source, problem.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pass_cancelled_by_shutdown_returns_instead_of_throwing()
    {
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        var result = await _c.Service.Handle(new CollectSessionsRequest(TimeSpan.FromSeconds(30)), shutdown.Token);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Good);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pass_shows_a_recycled_pid_as_an_unidentified_process()
    {
        ClaudeRuns(ticks: 999);   // the record says 100
        _ = _c.Records.With(LiveRecord());

        _ = await _c.Pass();

        var session = Assert.Single(_c.Snapshot.Current!.Sessions);
        Assert.Equal(("claude-code/pid-10-999", false), (session.Ref.ToString(), session.IdKnown));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pass_keeps_the_previous_snapshot_and_ledgers_nothing_when_the_process_table_fails()
    {
        ClaudeRuns();
        _ = _c.Records.With(LiveRecord());
        _ = await _c.Pass();
        var tracker = _c.Memory.Tracker;

        _c.Fake.Fail();
        var failed = await _c.After(TimeSpan.FromSeconds(2));

        Assert.Equal((false, 0), (failed.Good, failed.Changes.Count));
        _ = Assert.Single(_c.Snapshot.Current!.Sessions);
        Assert.Contains(_c.Snapshot.Current.Problems, static p => string.Equals(p.Code, "SND-SESSION-002", StringComparison.Ordinal) && string.Equals(p.Source, "processes", StringComparison.Ordinal) && p.Text.EndsWith("proc-unreadable", StringComparison.Ordinal));
        Assert.Same(tracker, _c.Memory.Tracker);
        _c.Fake.Heal();
        Assert.DoesNotContain((await _c.After(TimeSpan.FromSeconds(2))).Changes, static c => c.Kind == FleetChangeKind.Gone);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pass_reports_a_failed_source_and_keeps_the_others()
    {
        ClaudeRuns();
        _c.Records.Fail();

        _ = await _c.Pass();

        Assert.Equal("claude-code/pid-10-100", Assert.Single(_c.Snapshot.Current!.Sessions).Ref.ToString());
        var problem = Assert.Single(_c.Snapshot.Current.Problems);
        Assert.Equal(("claude-records", "SND-SESSION-001"), (problem.Source, problem.Code));
        Assert.DoesNotContain("unreadable", problem.Text, StringComparison.Ordinal);   // a fixed sentence, never an exception message
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_test_override_is_one_problem_row_beside_a_normal_list()
    {
        var c = new CollectorHarness(SessionSettings.Defaults with { TestOverride = true });
        _ = c.Fake.With(Proc(10, 1, "claude", argv0: "claude"));

        _ = await c.Pass();

        _ = Assert.Single(c.Snapshot.Current!.Sessions);
        Assert.Equal(["SND-SESSION-003"], c.Snapshot.Current.Problems.Select(static p => p.Code), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_disabled_collector_shows_only_the_disabled_problem()
    {
        var c = new CollectorHarness(SessionSettings.Defaults with { Enabled = false });
        _ = c.Fake.With(Proc(10, 1, "claude", argv0: "claude"));

        var response = await c.Pass();

        Assert.False(response.Good);
        Assert.Empty(c.Snapshot.Current!.Sessions);
        var problem = Assert.Single(c.Snapshot.Current.Problems);
        Assert.Equal(("SND-SESSION-002", true), (problem.Code, problem.Text.EndsWith("disabled", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pass_skips_the_toolkits_own_claude_p_calls()
    {
        ClaudeRuns();
        _ = _c.Fake.With(Proc(11, 10, "claude", argv0: "claude", tty: 0), Env(("AW_JUDGE_CHILD", "1"))).With(Proc(12, 1, "claude", argv0: "claude", tty: 0));

        _ = await _c.Pass();

        Assert.Equal(["claude-code/pid-10-100"], _c.Snapshot.Current!.Sessions.Select(static s => s.Ref.ToString()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pass_takes_launch_and_lane_from_the_allowlisted_environment_only()
    {
        _ = _c.Fake.With(Proc(10, 1, "claude", argv0: "claude"), Env(("AW_SINDRI_LAUNCH", "LCH-00000000000000000000000000000001"), ("AW_META_TEAM", "t1"), ("AW_META_LANE", "api"), ("ANTHROPIC_API_KEY", ProcessTableContract.PlantedToken)));

        _ = await _c.Pass();

        var session = Assert.Single(_c.Snapshot.Current!.Sessions);
        Assert.Equal(("LCH-00000000000000000000000000000001", "t1", "api"), (session.LaunchId!.Value.Value, session.Team, session.Lane));
        Assert.DoesNotContain(ProcessTableContract.PlantedToken, System.Text.Json.JsonSerializer.Serialize(session.Host) + session.Cwd + session.Title + session.Team + session.Lane, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Titles_are_scrubbed_and_capped_at_80_characters()
    {
        var c = new CollectorHarness(scrubber: new ReplacingScrubber("hunter2", "[secret]"));
        _ = c.Fake.With(Proc(10, 1, "claude", argv0: "claude"));
        _ = c.Records.With(LiveRecord() with { Name = "use hunter2 " + new string('x', 200) });

        _ = await c.Pass();

        var title = Assert.Single(c.Snapshot.Current!.Sessions).Title!;
        Assert.Equal((80, true), (title.Length, title.StartsWith("use [secret] xxx", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_that_leaves_a_good_pass_is_kept_ten_minutes_as_recently_ended()
    {
        ClaudeRuns();
        _ = _c.Records.With(LiveRecord());
        _ = await _c.Pass();

        _c.Fake.Exit(10);
        _ = await _c.After(TimeSpan.FromSeconds(2));
        var ended = Assert.Single(_c.Snapshot.Current!.RecentlyEnded);
        _ = await _c.After(TimeSpan.FromMinutes(11));

        Assert.Equal($"claude-code/{Id}", ended.Session.Ref.ToString());
        Assert.Empty(_c.Snapshot.Current.Sessions);
        Assert.Empty(_c.Snapshot.Current.RecentlyEnded);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_good_pass_asks_the_fleet_dir_to_prune_with_the_live_sessions()
    {
        ClaudeRuns();
        _ = _c.Records.With(LiveRecord());

        var response = await _c.Pass();

        var live = Assert.Single(Assert.Single(_c.Fleet.Prunes));
        Assert.Equal(($"claude-code/{Id}", 0), (live.ToString(), response.Pruned));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_blocked_records_source_and_a_blocked_fleet_source_are_one_problem_row_each_while_the_process_table_sessions_stay_live()
    {
        using var blocked = new BlockingSources();
        var c = new CollectorHarness(records: blocked, fleet: blocked);
        var budget = TimeSpan.FromSeconds(1);
        _ = c.Fake.With(Proc(10, 1, "claude", argv0: "claude", ticks: 100), cwd: "/w/api");

        var first = c.Pass(budget);
        await blocked.Records.Reached(1);
        c.Base.Clock.Advance(budget);   // the records read is late
        await blocked.Fleet.Reached(1);
        c.Base.Clock.Advance(budget);   // so is the fleet read
        var firstResponse = await first;
        _ = c.Fake.With(Proc(11, 1, "claude", argv0: "claude", ticks: 101), cwd: "/w/web");
        var secondResponse = await c.Pass(budget);   // both are still blocked: not started again, so there is nothing to wait for
        blocked.Release();

        Assert.Equal((true, false, 1), (firstResponse.Good, firstResponse.Overran, firstResponse.Sessions));
        Assert.Equal((true, false, 2), (secondResponse.Good, secondResponse.Overran, secondResponse.Sessions));
        var problems = c.Snapshot.Current!.Problems;
        Assert.Equal("claude-records,fleet-states", string.Join(',', problems.Select(static p => p.Source).Order(StringComparer.Ordinal)));
        Assert.All(problems, static p => Assert.Equal("SND-SESSION-001", p.Code));
        Assert.Equal((1, 1), (blocked.RecordReads, blocked.FleetReads));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pass_over_its_budget_keeps_the_previous_snapshot_and_says_so()
    {
        using var blocking = new BlockingProcessTable();
        var c = new CollectorHarness(processes: blocking);
        var budget = TimeSpan.FromSeconds(1);

        var pass = c.Pass(budget);
        await blocking.Reads.Reached(1);
        c.Base.Clock.Advance(budget);
        var response = await pass;
        blocking.Release();

        Assert.Equal((false, true), (response.Good, response.Overran));
        Assert.Equal("SND-SESSION-001", Assert.Single(c.Snapshot.Current!.Problems).Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pass_reads_the_state_from_the_hook_file_and_an_unknown_record_status_falls_back_to_the_transcript()
    {
        ClaudeRuns();
        _ = _c.Records.With(LiveRecord(status: "shell"));
        _ = _c.Transcripts.With(Id, _c.Now.AddSeconds(-5));

        _ = await _c.Pass();

        var session = Assert.Single(_c.Snapshot.Current!.Sessions);
        Assert.Equal((SessionState.Running, StateSource.Transcript, true), (session.State, session.Source, session.Estimated));
        _ = _c.Fleet.With(Hook(HookState.Asking, _c.Now, AskingReason.Approval, "Bash") with { Session = SessionId.From(Id) });
        _ = await _c.After(TimeSpan.FromSeconds(2));
        var asking = Assert.Single(_c.Snapshot.Current.Sessions);
        Assert.Equal((SessionState.Asking, AskingReason.Approval, "Bash", StateSource.Hook), (asking.State, asking.Reason, asking.Tool, asking.Source));
    }
}
