using Aw.Meta.Core.Health;

namespace Aw.Meta.Tests.Health;

public sealed class ProcessStartTests
{
    private static readonly DateTimeOffset _recorded = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Lane", "unit")]
    public void A_process_that_started_before_the_file_was_written_is_its_owner() =>
        Assert.False(ProcessStart.IsReused(_recorded - TimeSpan.FromHours(3), _recorded));

    [Fact, Trait("Lane", "unit")]
    public void A_process_that_started_within_the_tolerance_after_the_file_is_still_its_owner() =>
        Assert.False(ProcessStart.IsReused(_recorded + ProcessStart.Tolerance, _recorded));

    [Fact, Trait("Lane", "unit")]
    public void A_process_that_started_past_the_tolerance_after_the_file_is_another_process() =>
        Assert.True(ProcessStart.IsReused(_recorded + ProcessStart.Tolerance + TimeSpan.FromSeconds(1), _recorded));

    [Fact, Trait("Lane", "unit")]
    public void The_tolerance_is_two_seconds_because_ps_and_proc_count_whole_seconds() =>
        Assert.Equal(TimeSpan.FromSeconds(2), ProcessStart.Tolerance);
}
