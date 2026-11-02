using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>The watchdog's own memory kill, for a platform whose kernel has no memory limit (macOS; SB-9b), over a fake clock, scope and accounting.</summary>
[Lane(TestLane.Unit)]
public sealed class LimitWatchdogMemoryTests
{
    private const long Mib = 1024 * 1024;
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(20);
    private readonly TimerSteppingClock _clock = new(KnownInstants.Epoch);
    private readonly FakeScopeControl _scope;
    private readonly FakeCgroupStats _stats = new();
    private readonly FakeMemoryKills _kills = new();
    private readonly FakeWrapCalls _calls;
    private readonly TaskCompletionSource _child = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public LimitWatchdogMemoryTests()
    {
        _calls = new FakeWrapCalls(_clock);
        _scope = new FakeScopeControl(_clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Task<LimitEnd?> Start(IMemoryKills? kills)
    {
        var limits = new WrapLimits("sindri-LCH-1.scope", WallGraceSeconds: 5, ParkMaxSeconds: 3600, MemoryMaxBytes: 64 * Mib, WallSeconds: 600);
        return new LimitWatchdog(_scope, _stats, _calls, _clock, limits, kills).Run(A.FirstLaunchId, new LaunchSecret("proof"), _child.Task, Token);
    }

    private async Task<LimitEnd?> Verdict(Task<LimitEnd?> run)
    {
        _ = _child.TrySetResult();
        return await run.WaitAsync(_deadline, TimeProvider.System, Token);
    }

    [Fact]
    public async Task Under_the_line_nothing_is_killed()
    {
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 63 * Mib, MemoryPeak: 63 * Mib, Pids: 3, CpuMicroseconds: 1);
        var run = Start(_kills);

        await _clock.RunFor(TimeSpan.FromSeconds(30), Token);

        Expect.Equal(0, _scope.Kills.Count);
        Expect.Equal(0, _kills.Units.Count);
        Expect.True(await Verdict(run) is null);
    }

    [Fact]
    public async Task At_the_line_the_group_is_killed_within_one_tick_and_the_end_is_the_memory_limit()
    {
        var run = Start(_kills);
        await _clock.RunFor(TimeSpan.FromSeconds(12), Token);
        var rose = _clock.GetUtcNow();
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 64 * Mib, MemoryPeak: 64 * Mib, Pids: 3, CpuMicroseconds: 1);

        await _clock.RunFor(TimeSpan.FromSeconds(6), Token);

        var (at, signal) = Expect.Only(_scope.Kills);
        Expect.Equal("SIGKILL", signal);
        Expect.True(at - rose <= TimeSpan.FromSeconds(5));
        Expect.Equal("sindri-LCH-1.scope", Expect.Only(_kills.Units));
        var end = await Verdict(run);
        Expect.Equal(LimitKind.Memory, end!.Kind);
        Expect.Equal(64 * Mib, end.Limit);
        Expect.Equal(64 * Mib, end.Peak);
    }

    [Fact]
    public async Task The_kill_is_sent_once_however_long_the_group_takes_to_go()
    {
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 90 * Mib, MemoryPeak: 90 * Mib, Pids: 3, CpuMicroseconds: 1);
        var run = Start(_kills);

        await _clock.RunFor(TimeSpan.FromSeconds(30), Token);

        Expect.Equal(1, _scope.Kills.Count);
        Expect.Equal(1, _kills.Units.Count);
        _ = await Verdict(run);
    }

    [Fact]
    public async Task Without_the_policy_the_watchdog_leaves_the_kill_to_the_kernel()
    {
        _stats.Sample = new CgroupSample(OomKills: 0, MemoryCurrent: 90 * Mib, MemoryPeak: 90 * Mib, Pids: 3, CpuMicroseconds: 1);
        var run = Start(kills: null);

        await _clock.RunFor(TimeSpan.FromSeconds(30), Token);

        Expect.Equal(0, _scope.Kills.Count);
        Expect.True(await Verdict(run) is null);
    }

    private sealed class FakeMemoryKills : IMemoryKills
    {
        private readonly List<string> _units = [];

        public IReadOnlyList<string> Units { get { lock (_units) { return [.. _units]; } } }

        public Task<bool> Record(string unit, CancellationToken cancellationToken)
        {
            lock (_units)
            {
                _units.Add(unit);
            }

            return Task.FromResult(true);
        }
    }
}
