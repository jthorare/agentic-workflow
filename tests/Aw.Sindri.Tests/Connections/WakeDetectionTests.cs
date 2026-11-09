using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class WakeDetectionTests
{
    private static readonly TimeSpan _tick = TimeSpan.FromSeconds(30);

    [Fact]
    public void A_wall_clock_jump_past_three_ticks_signals_a_wake_and_a_normal_delay_does_not()
    {
        var clock = new SplitClock(KnownInstants.Epoch);
        var detector = new WakeDetector(clock, _tick);

        Assert.False(detector.Observe());   // the first call only sets the baseline

        clock.Advance(_tick);
        Assert.False(detector.Observe());   // an on-time tick

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.False(detector.Observe());   // a slow tick: both clocks moved, so nothing slept

        clock.JumpWall(TimeSpan.FromHours(2));
        Assert.True(detector.Observe());    // the wall clock ran ahead of the monotonic one

        clock.Advance(_tick);
        Assert.False(detector.Observe());   // the baseline was reset by the wake
    }

    [Fact]
    public void The_gap_has_to_be_over_three_ticks()
    {
        var clock = new SplitClock(KnownInstants.Epoch);
        var detector = new WakeDetector(clock, _tick);
        _ = detector.Observe();

        clock.JumpWall(_tick * 3);
        Assert.False(detector.Observe());   // exactly 3 x tick is not over

        clock.JumpWall((_tick * 3) + TimeSpan.FromSeconds(1));
        Assert.True(detector.Observe());
    }
}
