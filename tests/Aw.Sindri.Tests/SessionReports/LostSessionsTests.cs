using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.SweepLostSessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SessionReports;

/// <summary>The silence sweep (API spec §8.1): a session with no heartbeat for 120 s ends as lost, once.</summary>
[Lane(TestLane.Unit)]
public sealed class LostSessionsTests
{
    private readonly SessionHarness _s = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LostSessions Sweeper() => new(_s.Liveness, _s.Events(), _s.Views.Slice.Clock, _s.Files, new LaunchViews(_s.Views.Views), _s.Views.Progress);

    [Fact]
    public async Task Silence_120s_writes_lost()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);

        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(119));
        var early = await Sweeper().Sweep(Token);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(1));
        var swept = await Sweeper().Sweep(Token);

        Expect.Equal((0, 1), (early, swept));
        var recorded = Assert.Single(_s.Views.Slice.Store.Events);
        var payload = _s.Views.Slice.Events.Payload<SessionEnded>(recorded).Value;
        Expect.Equal(($"session.ended:{A.FirstLaunchId.Value}", SessionEndReason.Lost, null), (recorded.IdempotencyKey, payload.Reason, payload.ExitCode));
        Expect.Equal(A.FirstLaunchId, Assert.Single(_s.Files.Forgotten));
    }

    [Fact]
    public async Task A_lost_session_is_ended_once()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromMinutes(5));

        _ = await Sweeper().Sweep(Token);
        var again = await Sweeper().Sweep(Token);

        Expect.Equal(0, again);
        _ = Assert.Single(_s.Views.Slice.Store.Events);
    }

    [Fact]
    public async Task A_heartbeat_keeps_the_session()
    {
        await _s.Started();
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(100));
        _s.Liveness.Beat(A.FirstLaunchId, _s.Now);
        _s.Views.Slice.Clock.Advance(TimeSpan.FromSeconds(100));

        var swept = await Sweeper().Sweep(Token);

        Expect.Equal(0, swept);
        Expect.Equal(0, _s.Views.Slice.Store.Events.Count);
    }

    [Fact]
    public async Task The_start_sweep_keeps_the_files_of_a_live_session_and_removes_a_dead_ones()
    {
        await _s.Started();   // A.FirstLaunchId has a live session
        var dead = LaunchId.From("LCH-" + new string('0', 31) + "9");
        _s.Files.Open(A.FirstLaunchId);
        _s.Files.Open(dead);

        var removed = await Sweeper().SweepFiles(TimeSpan.FromSeconds(60), Token);

        Expect.Equal(1, removed);
        Expect.Equal(dead, Assert.Single(_s.Files.Swept));
    }

    [Fact]
    public async Task The_start_sweep_waits_for_the_views_to_catch_up()
    {
        await _s.Started();
        _s.Files.Open(LaunchId.From("LCH-" + new string('0', 31) + "9"));
        _s.Views.Publish(stalledAt: 1);

        Assert.Null(await Sweeper().SweepFiles(TimeSpan.FromSeconds(60), Token));
        Assert.Empty(_s.Files.Swept);
    }
}
