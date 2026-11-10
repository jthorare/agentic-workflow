using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;
using Aw.Meta.Features.LaneRecycle;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneRecycle;

// #2498 part 1: the meta lane is measured like every Claude lane, and it recycles only through a detached waiter that waits for the meta's own stop beat.
public sealed class MetaLaneRecycleTests
{
    private const string Digest = "/home/x/digests/t1-meta-2026-11-09.md";
    private const string OldSession = "s-meta-old";
    private const int WaiterPid = 4242;
    private static readonly TimeSpan _giveUpAfter = TimeSpan.FromMinutes(30);
    private readonly MetaWorld _world = new();
    private readonly InMemoryWaiterLauncher _launcher = new();
    private readonly InMemoryProcessLiveness _liveness = new();

    private async Task Ready()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("meta", SessionId.From(OldSession), Agent: null, A.At(1)));
        _world.LaneSessions.Set("t1-meta", OldSession);
        _world.Panes.Put("t1-meta", "claude");
        _world.ClaudeTranscripts.PutUsage(OldSession, 410_000, A.At(30));
        _world.Digests.Put(Digest, 1200, _world.Clock.GetUtcNow().AddMinutes(-5));
    }

    private LaneRecycleService Service(IDelay? delay = null)
    {
        var wait = delay ?? _world.Delay;
        var runner = new RecycleRunner(new PaneSubmitter(_world.Panes, wait), _world.LaneSessions, _world.LaneFiles, _world.Log, wait, _world.Clock, _world.Messages);
        return new LaneRecycleService(
            _world.Config,
            new RecycleGuard(_world.LaneFiles, _world.LaneSessions, _world.Digests, _world.Context, _world.Clock),
            runner,
            new SwitchRunner(new PaneSubmitter(_world.Panes, wait), _world.LaneSessions, _world.Runner, _world.Log, wait, _world.Clock),
            new RecycleWaiter(_launcher, _world.LaneFiles, _liveness, new LaneBeats(_world.Values, wait), runner, _world.Log, _world.Clock));
    }

    private StopCheckService StopCheck() => new(_world.Session, _world.Sweep, _world.Guard, _world.Clock, _world.Beats);

    private async Task<StopCheckResponse> MetaStops()
    {
        var result = await StopCheck().Handle(new StopCheckRequest(SessionId.From(MetaWorld.MetaSessionId), MetaWorld.Transcript, HookActive: false, MaxBlocks: 3), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task<Kernel.Result<LaneRecycleResponse>> Recycle(bool detach = false, bool waitIdle = false, string digest = Digest, IDelay? delay = null) =>
        await Service(delay).Handle(new LaneRecycleRequest("meta", digest, DryRun: false, detach, waitIdle), CancellationToken.None);

    private static LaneContext Meta(long tokens) => new("meta", SessionId.From(OldSession), "/t", tokens, LastActivity: null, 300_000, ContextVerdict.Recycle);

    // a
    [Fact, Trait("Lane", "unit")]
    public async Task The_context_reader_returns_a_row_for_the_meta_lane()
    {
        await Ready();
        var team = (await _world.Config.Load(CancellationToken.None)).Value;

        var rows = (await _world.Context.Read(team, CancellationToken.None)).Value;

        var meta = Assert.Single(rows, r => r.Lane == "meta");
        Assert.Equal((410_000L, ContextVerdict.Recycle), (meta.Tokens, meta.Verdict));
    }

    // b
    [Fact, Trait("Lane", "unit")]
    public void The_meta_crossing_the_threshold_prints_one_META_RECYCLE_line_per_crossing_instead_of_CONTEXT_HIGH()
    {
        var (firstLines, high) = ContextWatch.Step(ContextWatch.Parse(null), [Meta(410_000)], metaLane: "meta");
        var (secondLines, _) = ContextWatch.Step(high, [Meta(420_000)], metaLane: "meta");

        Assert.Equal(["META RECYCLE 410k"], firstLines);
        Assert.Empty(secondLines);
    }

    // c
    [Fact, Trait("Lane", "unit")]
    public async Task An_unblocked_meta_stop_records_a_stopped_beat_for_the_meta_lane()
    {
        await Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, string.Empty);

        var response = await MetaStops();

        var beat = (await _world.Beats.Last("meta", CancellationToken.None)).Value;
        Assert.False(response.Blocked);
        Assert.True(beat?.Stopped ?? false);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_meta_stop_blocked_by_an_open_obligation_records_no_stopped_beat()
    {
        await Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "please rebase"));

        var response = await MetaStops();

        var beat = (await _world.Beats.Last("meta", CancellationToken.None)).Value;
        Assert.True(response.Blocked);
        Assert.False(beat?.Stopped ?? false);
        Assert.NotNull(beat);   // the blocked stop is still a beat: the hook ran, the turn did not end
    }

    // d1
    [Fact, Trait("Lane", "unit")]
    public async Task The_meta_lane_without_detach_is_refused_with_AW_META_021_naming_detach()
    {
        await Ready();

        var result = await Recycle(detach: false);

        Assert.Equal(MetaErrors.RecycleRefused.Code, result.Error.Code);
        Assert.Contains("--detach", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_launcher.Started);
        Assert.Empty(_world.Panes.Woken);
    }

    // d2
    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_or_missing_digest_is_refused_before_any_waiter_starts()
    {
        await Ready();
        _world.Digests.Put("/old.md", 10, _world.Clock.GetUtcNow().AddHours(-3));

        foreach (var path in new[] { "/missing.md", "/old.md" })
        {
            var result = await Recycle(detach: true, digest: path);
            Assert.Equal(MetaErrors.RecycleRefused.Code, result.Error.Code);
            Assert.Contains(path, result.Error.Message, StringComparison.Ordinal);
        }

        Assert.Empty(_launcher.Started);
        Assert.Null((await _world.LaneFiles.Waiter("meta", CancellationToken.None)).Value);
    }

    // d3
    [Fact, Trait("Lane", "unit")]
    public async Task A_live_waiter_recorded_for_the_lane_refuses_a_second_one_with_AW_META_021()
    {
        await Ready();
        var started = _world.Clock.GetUtcNow();
        _liveness.Run(777, started.AddMinutes(-1));
        _ = await _world.LaneFiles.RecordWaiter("meta", (777, started), CancellationToken.None);

        var result = await Recycle(detach: true);

        Assert.Equal(MetaErrors.RecycleRefused.Code, result.Error.Code);
        Assert.Empty(_launcher.Started);
        Assert.Equal(777, (await _world.LaneFiles.Waiter("meta", CancellationToken.None)).Value!.Value.Pid);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recorded_waiter_pid_that_is_dead_does_not_count_and_a_new_waiter_starts()
    {
        await Ready();
        _ = await _world.LaneFiles.RecordWaiter("meta", (888, _world.Clock.GetUtcNow().AddMinutes(-20)), CancellationToken.None);

        var result = await Recycle(detach: true);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        _ = Assert.Single(_launcher.Started);
        Assert.Equal(WaiterPid, (await _world.LaneFiles.Waiter("meta", CancellationToken.None)).Value!.Value.Pid);
    }

    // d4
    [Fact, Trait("Lane", "unit")]
    public async Task Detach_starts_a_waiter_with_wait_idle_records_its_pid_and_types_nothing()
    {
        await Ready();

        var result = await Recycle(detach: true);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(WaiterPid, result.Value.WaiterPid);
        var argv = Assert.Single(_launcher.Started);
        Assert.Contains("lane-recycle", argv, StringComparer.Ordinal);
        Assert.Contains("meta", argv, StringComparer.Ordinal);
        Assert.Contains("--wait-idle", argv, StringComparer.Ordinal);
        Assert.DoesNotContain("--detach", argv, StringComparer.Ordinal);
        Assert.Equal(Digest, argv[argv.ToList().IndexOf("--digest") + 1]);
        Assert.Equal(WaiterPid, (await _world.LaneFiles.Waiter("meta", CancellationToken.None)).Value!.Value.Pid);
        Assert.Empty(_world.Panes.Woken);
    }

    // e1
    [Fact, Trait("Lane", "unit")]
    public async Task The_waiter_recycles_once_a_stop_beat_newer_than_its_start_arrives_and_clears_its_pid()
    {
        await Ready();
        var start = _world.Clock.GetUtcNow();
        _ = await _world.Beats.Record("meta", stopped: true, start.AddMinutes(-10), CancellationToken.None);   // the stop before the waiter existed does not count
        _ = await _world.LaneFiles.RecordWaiter("meta", (WaiterPid, start), CancellationToken.None);
        var wokenWhenTheStopArrived = -1;
        var delay = new HookingDelay(_world.Delay, _world.Clock, async (call, now) =>
        {
            if (call == 3)
            {
                wokenWhenTheStopArrived = _world.Panes.Woken.Count;
                _ = await _world.Beats.Record("meta", stopped: true, now, CancellationToken.None);
            }

            if (_world.Panes.Woken.Count > 0)
            {
                _world.LaneSessions.Set("t1-meta", "s-meta-new");
            }
        });

        var result = await Recycle(waitIdle: true, delay: delay);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Equal(0, wokenWhenTheStopArrived);
        Assert.Equal([("t1-meta", "/clear"), ("t1-meta", $"Read and continue from {Digest}")], _world.Panes.Woken);
        Assert.Equal("s-meta-new", (await _world.LaneFiles.Record("meta", CancellationToken.None)).Value!.Session!.Value.Value);
        Assert.Null((await _world.LaneFiles.Waiter("meta", CancellationToken.None)).Value);
    }

    // e2
    [Fact, Trait("Lane", "unit")]
    public async Task The_waiter_gives_up_after_30_minutes_when_an_open_obligation_keeps_the_stop_blocked()
    {
        await Ready();
        await _world.Receive("m1", "t1-api", "please rebase");
        var start = _world.Clock.GetUtcNow();
        _ = await _world.LaneFiles.RecordWaiter("meta", (WaiterPid, start), CancellationToken.None);
        var delay = new HookingDelay(_world.Delay, _world.Clock, async (call, _) =>
        {
            if (call % 100 == 1)
            {
                Assert.True((await MetaStops()).Blocked);
            }
        });

        _ = await Recycle(waitIdle: true, delay: delay);

        Assert.Empty(_world.Panes.Woken);
        Assert.True(_world.Clock.GetUtcNow() - start >= _giveUpAfter);
        Assert.Contains(_world.Log.Lines, l => l.Contains("gave up", StringComparison.Ordinal));
        Assert.Null((await _world.LaneFiles.Waiter("meta", CancellationToken.None)).Value);
        Assert.Equal(OldSession, (await _world.LaneFiles.Record("meta", CancellationToken.None)).Value!.Session!.Value.Value);
    }
}
