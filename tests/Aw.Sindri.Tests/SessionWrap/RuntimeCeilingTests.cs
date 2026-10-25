using Aw.Kernel.Testing;
using Aw.Sindri.Testing;
using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>The scope's loose ceiling (sandbox spec SB7, R3-03): never the clock, and moved forward on every resume so a long park never costs the run its scope.</summary>
[Lane(TestLane.Unit)]
public sealed class RuntimeCeilingTests
{
    private static readonly TimeSpan _wall = TimeSpan.FromMinutes(250);   // a 4 h Step timeout plus the 10 minute grace
    private static readonly TimeSpan _parkMax = TimeSpan.FromHours(24);

    [Fact]
    public void At_the_start_the_ceiling_is_the_wall_plus_the_longest_park() =>
        Expect.Equal(_wall + _parkMax, RuntimeCeiling.Next(TimeSpan.Zero, _wall, TimeSpan.Zero, _parkMax));

    [Fact]
    public void Park_longer_than_wallGrace_keeps_the_scope()
    {
        var elapsed = TimeSpan.FromHours(1) + TimeSpan.FromHours(12);   // one hour of work, then a twelve hour park
        var ceiling = RuntimeCeiling.Next(elapsed, _wall, TimeSpan.FromHours(1), _parkMax);

        Expect.True(ceiling > elapsed + (_wall - TimeSpan.FromHours(1)));   // the scope outlives the run's remaining wall time
        Expect.Equal(elapsed + (_wall - TimeSpan.FromHours(1)) + _parkMax, ceiling);
    }

    [Fact]
    public void Several_long_parks_in_a_row_never_cost_the_run_its_scope()
    {
        var elapsed = TimeSpan.Zero;
        var used = TimeSpan.Zero;
        for (var park = 0; park < 5; park++)
        {
            elapsed += TimeSpan.FromMinutes(20) + TimeSpan.FromHours(20);   // twenty minutes of work, then a twenty hour park
            used += TimeSpan.FromMinutes(20);
            var ceiling = RuntimeCeiling.Next(elapsed, _wall, used, _parkMax);

            Expect.True(ceiling - elapsed >= _parkMax);   // after each resume the scope still has at least one whole park ahead
        }
    }
}
