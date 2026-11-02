using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The learn window (sandbox spec §3.6): ten runs or seven days, or the owner ends it.</summary>
public sealed class LearnWindowTests
{
    private static readonly DateTimeOffset _first = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact, Lane(TestLane.Unit)]
    public void No_history_learns_for_seven_days_from_now()
    {
        var (mode, until) = LearnWindow.Decide(NetworkMode.Learn, new LearnHistory(FirstLearnRun: null, LearnRuns: 0, EndedAt: null), _first);

        Assert.Equal(NetworkMode.Learn, mode);
        Assert.Equal(_first.AddDays(7), until);
    }

    [Theory, Lane(TestLane.Unit)]
    [InlineData(9, NetworkMode.Learn)]
    [InlineData(10, NetworkMode.Enforce)]
    public void The_tenth_learn_run_closes_the_window(int runs, NetworkMode expected)
    {
        var (mode, _) = LearnWindow.Decide(NetworkMode.Learn, new LearnHistory(_first, runs, EndedAt: null), _first.AddDays(1));

        Assert.Equal(expected, mode);
    }

    [Theory, Lane(TestLane.Unit)]
    [InlineData(7, -1, NetworkMode.Learn)]
    [InlineData(7, 0, NetworkMode.Enforce)]
    public void Seven_days_after_the_first_learn_run_closes_the_window(int days, int offsetSeconds, NetworkMode expected)
    {
        var now = _first.AddDays(days).AddSeconds(offsetSeconds);

        var (mode, until) = LearnWindow.Decide(NetworkMode.Learn, new LearnHistory(_first, LearnRuns: 3, EndedAt: null), now);

        Assert.Equal(expected, mode);
        Assert.Equal(expected == NetworkMode.Learn ? _first.AddDays(7) : null, until);
    }

    [Fact, Lane(TestLane.Unit)]
    public void The_owner_ending_learn_mode_enforces()
    {
        var (mode, until) = LearnWindow.Decide(NetworkMode.Learn, new LearnHistory(_first, 2, _first.AddHours(1)), _first.AddHours(2));

        Assert.Equal(NetworkMode.Enforce, mode);
        Assert.Null(until);
    }

    [Theory, Lane(TestLane.Unit)]
    [InlineData(NetworkMode.Enforce)]
    [InlineData(null)]
    public void A_profile_that_enforces_never_learns(NetworkMode? profile)
    {
        var (mode, until) = LearnWindow.Decide(profile, new LearnHistory(FirstLearnRun: null, LearnRuns: 0, EndedAt: null), _first);

        Assert.Equal(NetworkMode.Enforce, mode);
        Assert.Null(until);
    }
}
