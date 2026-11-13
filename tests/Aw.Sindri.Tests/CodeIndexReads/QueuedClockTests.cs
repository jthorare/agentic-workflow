using Aw.Sindri.Features.CodeIndexReads;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>How long a repo has read queued (#3440): counted from the first read that saw it queued, per repo, and forgotten when a read sees it leave.</summary>
public sealed class QueuedClockTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Lane", "unit")]
    public void The_first_queued_read_starts_the_count_at_zero() =>
        Assert.Equal(TimeSpan.Zero, new QueuedClock().For("demo", queued: true, _t0));

    [Fact, Trait("Lane", "unit")]
    public void A_later_queued_read_counts_from_the_first()
    {
        var clock = new QueuedClock();
        _ = clock.For("demo", queued: true, _t0);

        Assert.Equal(TimeSpan.FromMinutes(31), clock.For("demo", queued: true, _t0.AddMinutes(31)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_read_that_is_not_queued_is_null_and_starts_the_count_again()
    {
        var clock = new QueuedClock();
        _ = clock.For("demo", queued: true, _t0);

        Assert.Null(clock.For("demo", queued: false, _t0.AddMinutes(20)));
        Assert.Equal(TimeSpan.Zero, clock.For("demo", queued: true, _t0.AddMinutes(40)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_repo_has_its_own_count()
    {
        var clock = new QueuedClock();
        _ = clock.For("demo", queued: true, _t0);

        Assert.Equal(TimeSpan.Zero, clock.For("other", queued: true, _t0.AddMinutes(31)));
    }
}
