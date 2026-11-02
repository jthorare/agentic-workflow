using Aw.Context.Core.Records;

namespace Aw.Context.Tests.Sync;

/// <summary>The backoff rule of a record source (#1597 part A): 5 minutes after the first failure, doubling, never over 6 hours; a success or the time cap resets it.</summary>
[Trait("Lane", "unit")]
public sealed class SourceBackoffTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(7, 320)]
    [InlineData(8, 360)]
    [InlineData(40, 360)]
    public void The_wait_doubles_from_5_minutes_and_stops_at_6_hours(int failures, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), SourceBackoff.Delay(failures));

    [Fact]
    public void A_first_failure_counts_one_and_sets_the_next_try_5_minutes_out()
    {
        var next = SourceBackoff.Next(previousFailures: 0, new SyncAttempt(_at, Failed: true, "down"));

        Assert.Equal(new SourceBackoffState(Failures: 1, NextTry: _at.AddMinutes(5), LastCommit: null, LastErrorText: "down"), next);
    }

    [Fact]
    public void A_second_failure_waits_10_minutes()
    {
        var next = SourceBackoff.Next(previousFailures: 1, new SyncAttempt(_at, Failed: true, "down"));

        Assert.Equal((2, _at.AddMinutes(10)), (next.Failures, next.NextTry));
    }

    [Fact]
    public void A_success_clears_the_failures_the_next_try_and_the_error_and_stamps_the_commit()
    {
        var next = SourceBackoff.Next(previousFailures: 4, new SyncAttempt(_at, Failed: false, ErrorText: null));

        Assert.Equal(new SourceBackoffState(Failures: 0, NextTry: null, LastCommit: _at, LastErrorText: null), next);
    }

    [Fact]
    public void A_failure_keeps_the_last_commit_and_a_success_replaces_it()
    {
        var before = new SourceBackoffState(Failures: 0, NextTry: null, LastCommit: _at.AddHours(-1), LastErrorText: null);

        var failed = SourceBackoff.Apply(before, new SyncAttempt(_at, Failed: true, "down"));
        var healed = SourceBackoff.Apply(failed, new SyncAttempt(_at.AddHours(1), Failed: false, ErrorText: null));

        Assert.Equal(_at.AddHours(-1), failed.LastCommit);
        Assert.Equal((0, (DateTimeOffset?)_at.AddHours(1)), (healed.Failures, healed.LastCommit));
    }

    [Fact]
    public void The_pull_word_follows_running_then_backoff_then_five_failures()
    {
        var failing = new SourceBackoffState(Failures: 3, NextTry: _at.AddMinutes(20), LastCommit: null, LastErrorText: "down");

        Assert.Equal("running", SourceBackoff.Pull(failing, running: true, _at));
        Assert.Equal("backoff", SourceBackoff.Pull(failing, running: false, _at.AddMinutes(19)));
        Assert.Equal("idle", SourceBackoff.Pull(failing, running: false, _at.AddMinutes(20)));
        Assert.Equal("failed", SourceBackoff.Pull(failing with { Failures = 5 }, running: false, _at.AddMinutes(20)));
        Assert.Equal("idle", SourceBackoff.Pull(SourceBackoffState.None, running: false, _at));
    }
}
