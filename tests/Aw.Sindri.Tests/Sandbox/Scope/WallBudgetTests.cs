using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Scope;

/// <summary>The wall clock counts unsuspended time only, and the loose ceiling follows it (sandbox spec §3.3).</summary>
[Lane(TestLane.Unit)]
public sealed class WallBudgetTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int minutes) => _t0.AddMinutes(minutes);

    [Fact]
    public void An_interval_counts_only_when_unsuspended_at_both_ends()
    {
        var b = new WallBudget(TimeSpan.FromMinutes(60));
        _ = b.Tick(At(0), suspended: false);
        _ = b.Tick(At(5), suspended: false);     // counts 5
        _ = b.Tick(At(10), suspended: true);     // ends unsuspended->suspended: not counted
        _ = b.Tick(At(40), suspended: true);     // suspended both ends
        _ = b.Tick(At(45), suspended: false);    // suspended->unsuspended: not counted
        _ = b.Tick(At(50), suspended: false);    // counts 5

        Assert.Equal(TimeSpan.FromMinutes(10), b.Used);
        Assert.Equal(TimeSpan.FromMinutes(50), b.Left);
    }

    [Fact]
    public void Wall_is_exhausted_after_unsuspended_time_reaches_it()
    {
        var b = new WallBudget(TimeSpan.FromMinutes(20));
        _ = b.Tick(At(0), suspended: false);
        _ = b.Tick(At(19), suspended: false);
        Assert.False(b.Exhausted);

        _ = b.Tick(At(20), suspended: false);

        Assert.True(b.Exhausted);
        Assert.Equal(TimeSpan.Zero, b.Left);
    }

    [Fact]
    public void Only_a_tick_that_leaves_a_suspension_signals_a_resume()
    {
        var b = new WallBudget(TimeSpan.FromMinutes(20));

        Assert.False(b.Tick(At(0), suspended: false));
        Assert.False(b.Tick(At(1), suspended: true));
        Assert.False(b.Tick(At(2), suspended: true));
        Assert.True(b.Tick(At(3), suspended: false));
        Assert.False(b.Tick(At(4), suspended: false));
    }

    [Fact]
    public void Park_longer_than_wallGrace_keeps_the_scope()
    {
        var wall = TimeSpan.FromMinutes(20);
        var parkMax = TimeSpan.FromHours(24);
        var b = new WallBudget(wall);
        _ = b.Tick(At(0), suspended: false);
        _ = b.Tick(At(5), suspended: false);
        _ = b.Tick(At(5).AddSeconds(5), suspended: true);       // parked: 30 minutes, longer than the 10 minute grace
        _ = b.Tick(At(35), suspended: true);
        var resumed = b.Tick(At(35).AddSeconds(5), suspended: false);

        var elapsedReal = At(35).AddSeconds(5) - At(0);
        var ceiling = RuntimeCeiling.Next(elapsedReal, b.Left, parkMax);

        Assert.True(resumed);
        Assert.InRange(b.Left, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        Assert.True(ceiling > elapsedReal);
        Assert.Equal(elapsedReal + b.Left + parkMax, ceiling);
    }

    [Fact]
    public void Ceiling_after_several_parks_is_elapsed_plus_left_plus_parkMax()
    {
        var b = new WallBudget(TimeSpan.FromMinutes(60));
        _ = b.Tick(At(0), suspended: false);
        _ = b.Tick(At(10), suspended: false);
        _ = b.Tick(At(11), suspended: true);
        _ = b.Tick(At(100), suspended: true);
        _ = b.Tick(At(101), suspended: false);
        _ = b.Tick(At(111), suspended: false);     // 20 min used
        _ = b.Tick(At(112), suspended: true);
        _ = b.Tick(At(500), suspended: true);
        _ = b.Tick(At(501), suspended: false);

        Assert.Equal(TimeSpan.FromMinutes(20), b.Used);
        Assert.Equal(TimeSpan.FromMinutes(501 + 40 + 60), RuntimeCeiling.Next(TimeSpan.FromMinutes(501), b.Left, TimeSpan.FromMinutes(60)));
    }
}
