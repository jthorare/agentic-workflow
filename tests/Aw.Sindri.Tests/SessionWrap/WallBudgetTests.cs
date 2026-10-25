using Aw.Kernel.Testing;
using Aw.Sindri.Testing;
using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>The session's wall clock (sandbox spec SB7, §3.3, §7): it counts only the time the run was not suspended, and a park or takeover stops it.</summary>
[Lane(TestLane.Unit)]
public sealed class WallBudgetTests
{
    private static readonly DateTimeOffset _t0 = KnownInstants.Epoch;
    private readonly WallBudget _budget = new(TimeSpan.FromMinutes(10));

    [Fact]
    public void Unsuspended_time_counts_from_the_second_look_on()
    {
        _budget.Tick(_t0, suspended: false);
        _budget.Tick(_t0.AddMinutes(3), suspended: false);

        Expect.Equal((TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(7), false), (_budget.Used, _budget.Left, _budget.Exceeded));
    }

    [Fact]
    public void Wall_clock_stops_while_suspended()
    {
        _budget.Tick(_t0, suspended: false);
        _budget.Tick(_t0.AddMinutes(2), suspended: false);
        _budget.Tick(_t0.AddMinutes(3), suspended: true);   // a park begins
        _budget.Tick(_t0.AddMinutes(60), suspended: true);   // an hour later it is still parked
        _budget.Tick(_t0.AddMinutes(61), suspended: false);   // the answer arrives

        Expect.Equal(TimeSpan.FromMinutes(2), _budget.Used);   // the minute the park began in and the minute it ended in are not counted
        Expect.True(!_budget.Exceeded);
    }

    [Fact]
    public void The_clock_resumes_with_what_remained_after_a_park()
    {
        _budget.Tick(_t0, suspended: false);
        _budget.Tick(_t0.AddMinutes(4), suspended: false);
        _budget.Tick(_t0.AddMinutes(5), suspended: true);
        _budget.Tick(_t0.AddMinutes(500), suspended: false);
        _budget.Tick(_t0.AddMinutes(505), suspended: false);

        Expect.Equal(TimeSpan.FromMinutes(9), _budget.Used);
        Expect.Equal(TimeSpan.FromMinutes(1), _budget.Left);
    }

    [Fact]
    public void The_limit_is_exceeded_when_the_unsuspended_time_reaches_it()
    {
        _budget.Tick(_t0, suspended: false);
        _budget.Tick(_t0.AddMinutes(10), suspended: false);

        Expect.True(_budget.Exceeded);
        Expect.Equal(TimeSpan.Zero, _budget.Left);
    }

    [Fact]
    public void A_clock_that_steps_back_counts_nothing()
    {
        _budget.Tick(_t0.AddMinutes(5), suspended: false);
        _budget.Tick(_t0, suspended: false);

        Expect.Equal(TimeSpan.Zero, _budget.Used);
    }
}
