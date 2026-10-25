using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>The one clock and the memory watch of a sandboxed session (sandbox spec §3.3, SB7, SB16) over a fake scope, a fake control plane and a fake clock.</summary>
[Lane(TestLane.Unit)]
public sealed class LimitWatchdogTests : IDisposable
{
    private const long Gib = 1024L * 1024 * 1024;
    private static readonly LaunchSecret _proof = new("wrap-proof");
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeScope _scope = new();
    private readonly FakeWrapCalls _calls;
    private readonly StringWriter _errors = new();
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _stop = new();
    private Task? _watch;

    public LimitWatchdogTests()
    {
        _calls = new FakeWrapCalls(_clock);
        _scope.OnKill = _ => _exited.TrySetResult();   // either signal ends the agent unless a test says otherwise
    }

    private static WrapLimits Limits(long? wall = 600) => new("sindri-unit.scope", wall, WallGraceSeconds: 60, ParkMaxSeconds: 86400, MemoryMaxBytes: 4 * Gib);

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
        _errors.Dispose();
    }

    private LimitWatchdog Start(WrapLimits limits, bool withProof = true)
    {
        var watchdog = new LimitWatchdog(limits, _scope, _calls, _clock, _errors);
        _watch = watchdog.Run(A.FirstLaunchId, withProof ? _proof : null, _exited.Task, _stop.Token);
        return watchdog;
    }

    private async Task Advance(TimeSpan span, TimeSpan? step = null)
    {
        var by = step ?? TimeSpan.FromSeconds(1);
        for (var elapsed = TimeSpan.Zero; elapsed < span; elapsed += by)
        {
            _clock.Advance(by);
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }
    }

    private async Task Finish()
    {
        _exited.TrySetResult();
        await _stop.CancelAsync();
        await _watch!.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_sleeper_past_its_wall_limit_is_stopped_and_ends_as_limit_wall()
    {
        var watchdog = Start(Limits(wall: 600));

        await Advance(TimeSpan.FromMinutes(11));
        await Finish();
        var ended = await watchdog.Conclude(TestContext.Current.CancellationToken);

        Expect.Sequence([false], _scope.Kills);
        Expect.True(ended is { Kind: LimitKind.Wall, Limit: 600 });
        Expect.True(ended!.Peak >= 600);
    }

    [Fact]
    public async Task Wall_clock_stops_while_suspended()
    {
        var parkedUntil = KnownInstants.Epoch.AddMinutes(30);
        _calls.Suspension = now => now < parkedUntil;
        _ = Start(Limits(wall: 600));

        await Advance(TimeSpan.FromMinutes(35));   // thirty minutes parked, five running
        var killedWhileParked = _scope.Kills.Count;
        await Advance(TimeSpan.FromMinutes(7));   // the remaining ten minutes are used up
        await Finish();

        Expect.Equal(0, killedWhileParked);
        Expect.Sequence([false], _scope.Kills);
    }

    [Fact]
    public async Task Unavailable_suspension_read_counts_as_not_suspended()
    {
        _calls.Suspension = static _ => SindriErrors.NotRunning.With("the control plane is down");
        _ = Start(Limits(wall: 600));

        await Advance(TimeSpan.FromMinutes(11));
        await Finish();

        Expect.Sequence([false], _scope.Kills);
    }

    [Fact]
    public async Task A_suspension_read_that_throws_counts_as_not_suspended()
    {
        _calls.Suspension = static _ => throw new HttpRequestException("connection refused");
        _ = Start(Limits(wall: 600));

        await Advance(TimeSpan.FromMinutes(11));
        await Finish();

        Expect.Sequence([false], _scope.Kills);
    }

    [Fact]
    public async Task A_session_without_a_launch_secret_is_never_suspended_and_asks_nothing()
    {
        _ = Start(Limits(wall: 600), withProof: false);

        await Advance(TimeSpan.FromMinutes(11));
        await Finish();

        Expect.Equal(0, _calls.SuspensionReads);
        Expect.Sequence([false], _scope.Kills);
    }

    [Fact]
    public async Task A_term_that_is_ignored_is_followed_by_kill_after_ten_seconds()
    {
        _scope.OnKill = force => _ = force && _exited.TrySetResult();   // SIGTERM is ignored; SIGKILL cannot be
        _ = Start(Limits(wall: 600));

        await Advance(TimeSpan.FromSeconds(600 + 5));
        var afterTerm = _scope.Kills.Count;
        await Advance(TimeSpan.FromSeconds(12));
        await Finish();

        Expect.Equal(1, afterTerm);
        Expect.Sequence([false, true], _scope.Kills);
    }

    [Fact]
    public async Task Without_a_wall_limit_nothing_is_stopped()
    {
        _ = Start(Limits(wall: null));

        await Advance(TimeSpan.FromHours(30), TimeSpan.FromMinutes(1));
        await Finish();

        Expect.Equal(0, _scope.Kills.Count);
    }

    [Fact]
    public async Task An_oom_kill_in_memory_events_ends_as_limit_memory_with_the_peak()
    {
        var watchdog = Start(Limits());
        _scope.Current = FakeScope.Reading(memory: 3 * Gib, peak: 4 * Gib, oomKill: 1);

        await Advance(TimeSpan.FromSeconds(6));
        await Finish();
        var ended = await watchdog.Conclude(TestContext.Current.CancellationToken);

        Expect.True(ended is { Kind: LimitKind.Memory } && ended.Limit == 4 * Gib && ended.Peak == 4 * Gib);
        Expect.Equal(0, _scope.Kills.Count);   // the kernel did the killing; the watchdog only names it
    }

    [Fact]
    public async Task An_oom_the_poll_missed_is_read_from_the_failed_scope()
    {
        var watchdog = Start(Limits());
        _scope.Current = FakeScope.Reading(memory: 2 * Gib, peak: 3 * Gib);
        await Advance(TimeSpan.FromSeconds(6));
        _scope.Current = null;   // the scope died between two ticks: its files are gone
        _scope.Ended = "oom-kill";

        await Finish();
        var ended = await watchdog.Conclude(TestContext.Current.CancellationToken);

        Expect.True(ended is { Kind: LimitKind.Memory } && ended.Limit == 4 * Gib && ended.Peak == 3 * Gib);
    }

    [Fact]
    public async Task A_scope_systemd_ended_at_its_ceiling_is_a_wall_limit()
    {
        var watchdog = Start(Limits(wall: 600));
        await Advance(TimeSpan.FromSeconds(30));
        _scope.Current = null;
        _scope.Ended = "timeout";

        await Finish();
        var ended = await watchdog.Conclude(TestContext.Current.CancellationToken);

        Expect.True(ended is { Kind: LimitKind.Wall, Limit: 600 });
    }

    [Fact]
    public async Task An_ordinary_exit_is_not_a_limit()
    {
        var watchdog = Start(Limits());
        await Advance(TimeSpan.FromSeconds(30));
        _scope.Current = null;

        await Finish();

        Expect.Equal(null, await watchdog.Conclude(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Peaks_are_the_highest_seen_over_the_session()
    {
        var watchdog = Start(Limits());
        _scope.Current = FakeScope.Reading(memory: 100, peak: 150, tasks: 5, cpuMicroseconds: 0);
        await Advance(TimeSpan.FromSeconds(6));
        _scope.Current = FakeScope.Reading(memory: 300, peak: 300, tasks: 9, cpuMicroseconds: 5_000_000);
        await Advance(TimeSpan.FromSeconds(5));
        _scope.Current = FakeScope.Reading(memory: 200, peak: 300, tasks: 3, cpuMicroseconds: 5_500_000, oomKill: 1);
        await Advance(TimeSpan.FromSeconds(5));

        await Finish();
        var peaks = (await watchdog.Conclude(TestContext.Current.CancellationToken))?.Peaks;

        Expect.True(peaks is not null && peaks.MemoryBytes == 300 && peaks.Tasks == 9 && peaks.CpuPercent >= 50 && peaks.WallSeconds >= 10);
    }

    [Fact]
    public async Task When_a_suspension_ends_the_loose_ceiling_moves_forward_by_the_parks_length()
    {
        _calls.Suspension = now => now < KnownInstants.Epoch.AddMinutes(10);
        _ = Start(Limits(wall: 600));

        await Advance(TimeSpan.FromMinutes(12));
        await Finish();

        var ceiling = Assert.Single(_scope.Ceilings);
        Expect.True(ceiling >= TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(86400));   // at least the park already spent plus a whole park to come
        Expect.True(ceiling <= TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(600) + TimeSpan.FromSeconds(86400));
    }

    [Fact]
    public async Task A_run_that_is_never_suspended_never_moves_the_ceiling()
    {
        _ = Start(Limits(wall: 600));

        await Advance(TimeSpan.FromMinutes(5));
        await Finish();

        Expect.Equal(0, _scope.Ceilings.Count);
    }
}
