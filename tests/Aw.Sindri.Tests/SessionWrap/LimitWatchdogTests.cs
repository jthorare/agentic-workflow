using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>The wall and memory watchdog of 'session wrap --limits' (sandbox spec §3.3, §15 item 4) over a fake clock, scope and accounting files.</summary>
[Lane(TestLane.Unit)]
public sealed class LimitWatchdogTests
{
    private const long Mib = 1024 * 1024;
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(20);
    private readonly TimerSteppingClock _clock = new(KnownInstants.Epoch);
    private readonly FakeWrapCalls _calls;
    private readonly FakeScopeControl _scope;
    private readonly FakeCgroupStats _stats = new();
    private readonly TaskCompletionSource _child = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public LimitWatchdogTests()
    {
        _calls = new FakeWrapCalls(_clock);
        _scope = new FakeScopeControl(_clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WrapLimits Limits(long? wall) => new("sindri-LCH-1.scope", WallGraceSeconds: 5, ParkMaxSeconds: 3600, MemoryMaxBytes: 64 * Mib, WallSeconds: wall);

    private Task<LimitEnd?> Start(WrapLimits limits) =>
        new LimitWatchdog(_scope, _stats, _calls, _clock, limits).Run(A.FirstLaunchId, new LaunchSecret("proof"), _child.Task, Token);

    // why: moves to each due timer in turn instead of a fixed tick with a real pause after it.
    private Task Advance(TimeSpan span) => _clock.RunFor(span, Token);

    private async Task<LimitEnd?> Verdict(Task<LimitEnd?> run)
    {
        _ = _child.TrySetResult();
        return await run.WaitAsync(_deadline, TimeProvider.System, Token);
    }

    [Fact]
    public async Task Unavailable_suspension_read_counts_as_not_suspended()
    {
        _calls.SuspendedUnavailable = true;
        var run = Start(Limits(wall: 20));

        await Advance(TimeSpan.FromSeconds(25));

        Expect.True(_scope.Kills.Any(k => k.Signal == "SIGTERM"));
        _ = await Verdict(run);
    }

    [Fact]
    public async Task The_clock_stops_while_the_run_is_suspended()
    {
        _calls.IsSuspended = true;
        var run = Start(Limits(wall: 20));

        await Advance(TimeSpan.FromSeconds(90));

        Expect.Equal(0, _scope.Kills.Count);
        Expect.True(await Verdict(run) is null);
    }

    [Fact]
    public async Task A_resume_moves_the_ceiling_to_elapsed_plus_left_plus_parkMax()
    {
        var start = _clock.GetUtcNow();
        _calls.IsSuspended = true;
        var run = Start(Limits(wall: 20));
        await Advance(TimeSpan.FromSeconds(12));
        Expect.Equal(0, _scope.Ceilings.Count);

        _calls.IsSuspended = false;
        await Advance(TimeSpan.FromSeconds(6));

        var (at, ceiling) = Expect.Only(_scope.Ceilings);
        Expect.Equal(at - start + TimeSpan.FromSeconds(20 + 3600), ceiling);   // nothing of the wall was used: the suspended ticks and the resuming one cost nothing
        _ = await Verdict(run);
    }

    [Fact]
    public async Task An_exhausted_wall_sends_TERM_then_KILL_after_10_seconds_and_reports_limit_wall()
    {
        var run = Start(Limits(wall: 20));

        await Advance(TimeSpan.FromSeconds(25));
        var (termAt, termSignal) = Expect.Only(_scope.Kills);
        Expect.Equal("SIGTERM", termSignal);

        await Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(["SIGTERM", "SIGKILL"], [.. _scope.Kills.Select(k => k.Signal)]);
        Expect.True(_scope.Kills[1].At - termAt >= TimeSpan.FromSeconds(10));

        var end = await Verdict(run);
        Expect.Equal(LimitKind.Wall, end!.Kind);
        Expect.Equal(20, end.Limit);
        Expect.True(end.Peak >= 20);
    }

    [Fact]
    public async Task An_agent_that_exits_on_TERM_is_not_sent_KILL()
    {
        var run = Start(Limits(wall: 20));
        await Advance(TimeSpan.FromSeconds(25));

        _child.SetResult();
        var end = await run.WaitAsync(_deadline, TimeProvider.System, Token);
        await Advance(TimeSpan.FromSeconds(15));

        Assert.Equal(["SIGTERM"], [.. _scope.Kills.Select(k => k.Signal)]);
        Expect.Equal(LimitKind.Wall, end!.Kind);
    }

    [Fact]
    public async Task An_attended_session_has_no_wall()
    {
        var run = Start(Limits(wall: null));

        await Advance(TimeSpan.FromSeconds(120));
        _calls.IsSuspended = true;
        await Advance(TimeSpan.FromSeconds(10));
        _calls.IsSuspended = false;
        await Advance(TimeSpan.FromSeconds(10));

        Expect.Equal(0, _scope.Kills.Count);
        Expect.Equal(0, _scope.Ceilings.Count);   // an attended scope has no RuntimeMaxSec to move
        Expect.True(await Verdict(run) is null);
    }

    [Fact]
    public async Task An_oom_kill_in_memory_events_reports_limit_memory_with_the_peak()
    {
        _stats.Sample = new CgroupSample(OomKills: 1, MemoryCurrent: 60 * Mib, MemoryPeak: 70 * Mib, Pids: 5, CpuMicroseconds: 1_000_000);
        var run = Start(Limits(wall: 600));

        await Advance(TimeSpan.FromSeconds(6));
        var end = await Verdict(run);

        Expect.Equal(LimitKind.Memory, end!.Kind);
        Expect.Equal(64 * Mib, end.Limit);
        Expect.Equal(70 * Mib, end.Peak);
        Expect.Equal(70 * Mib, end.Peaks!.MemoryBytes);
        Expect.Equal(5, end.Peaks.Tasks);
    }

    [Fact]
    public async Task A_missed_poll_still_reports_memory_from_the_units_result()
    {
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 30 * Mib, MemoryPeak: 30 * Mib, Pids: 2, CpuMicroseconds: 0);
        var run = Start(Limits(wall: 600));
        await Advance(TimeSpan.FromSeconds(6));

        _scope.UnitResult = "oom-kill";
        var end = await Verdict(run);

        Expect.Equal(LimitKind.Memory, end!.Kind);
        Expect.Equal(64 * Mib, end.Limit);
        Expect.Equal(30 * Mib, end.Peak);
    }

    [Fact]
    public async Task A_vanished_scope_keeps_the_last_sample()
    {
        _stats.Sample = new CgroupSample(OomKills: 1, MemoryCurrent: 63 * Mib, MemoryPeak: 64 * Mib, Pids: 3, CpuMicroseconds: 0);
        var run = Start(Limits(wall: 600));
        await Advance(TimeSpan.FromSeconds(1));

        _stats.Sample = null;   // the scope is gone
        await Advance(TimeSpan.FromSeconds(11));
        var end = await Verdict(run);

        Expect.Equal(LimitKind.Memory, end!.Kind);
        Expect.Equal(64 * Mib, end.Peak);
    }

    [Fact]
    public async Task An_ordinary_exit_reports_no_limit()
    {
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 10 * Mib, MemoryPeak: 12 * Mib, Pids: 1, CpuMicroseconds: 0);
        var run = Start(Limits(wall: 600));

        await Advance(TimeSpan.FromSeconds(6));

        Expect.True(await Verdict(run) is null);
    }

    [Fact]
    public async Task Refused_forks_are_recorded_once_across_ticks_and_never_kill()
    {
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 10 * Mib, MemoryPeak: 12 * Mib, Pids: 2040, CpuMicroseconds: 0, PidsMax: 3, PidsPeak: 2048);
        var run = Start(Limits(wall: null));

        await Advance(TimeSpan.FromSeconds(20));
        _stats.Sample = _stats.Sample with { PidsMax = 9 };
        await Advance(TimeSpan.FromSeconds(10));
        var verdict = await Verdict(run);

        Expect.Equal((LimitHitKind.Pids, 2048L), Assert.Single(_calls.Hits));
        Expect.True(_scope.Kills.Count == 0 && verdict is null);
    }

    [Fact]
    public async Task A_failed_record_is_tried_again_on_the_next_tick()
    {
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 10 * Mib, MemoryPeak: 12 * Mib, Pids: 2048, CpuMicroseconds: 0, PidsMax: 1, PidsPeak: 2048);
        _calls.LimitHitUnavailable = true;
        var run = Start(Limits(wall: null));
        await Advance(TimeSpan.FromSeconds(10));

        _calls.LimitHitUnavailable = false;
        await Advance(TimeSpan.FromSeconds(10));
        _ = await Verdict(run);

        _ = Assert.Single(_calls.Hits);
    }

    [Fact]
    public async Task No_refused_fork_records_nothing()
    {
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 10 * Mib, MemoryPeak: 12 * Mib, Pids: 30, CpuMicroseconds: 0);
        var run = Start(Limits(wall: null));

        await Advance(TimeSpan.FromSeconds(15));
        _ = await Verdict(run);

        Expect.Equal(0, _calls.Hits.Count);
    }
}
