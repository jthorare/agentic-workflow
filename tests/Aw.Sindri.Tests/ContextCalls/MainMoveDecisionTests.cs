using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The shared 'a repo's trunk moved' decision (F1): the context map refresh now, the code index build later, per repo.</summary>
public sealed class MainMoveDecisionTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, string> Heads(params (string Repo, string Sha)[] heads) => heads.ToDictionary(static h => h.Repo, static h => h.Sha, StringComparer.Ordinal);

    private static SyncAttempt Failed(int minutesAgo) => new(Failed: true, _now.AddMinutes(-minutesAgo));

    private static SyncAttempt Ok(int minutesAgo) => new(Failed: false, _now.AddMinutes(-minutesAgo));

    private static MainMoveAction Decide(Dictionary<string, string> current, Dictionary<string, string>? last, bool running = false, params SyncAttempt[] finished) =>
        MainMoveDecision.Decide(current, last, running, finished, _now);

    [Fact, Trait("Lane", "unit")]
    public void A_running_job_is_never_doubled_even_with_moved_heads() =>
        Assert.Equal(MainMoveAction.JobRunning, Decide(Heads(("a", "2")), Heads(("a", "1")), running: true));

    [Fact, Trait("Lane", "unit")]
    public void No_repo_on_its_default_branch_is_nothing() =>
        Assert.Equal(MainMoveAction.Nothing, Decide(Heads(), Heads(("a", "1"))));

    [Fact, Trait("Lane", "unit")]
    public void Nothing_to_do_while_a_job_runs_and_no_repo_is_on_trunk_is_the_running_job_first() =>
        Assert.Equal(MainMoveAction.JobRunning, Decide(Heads(), last: null, running: true));

    [Fact, Trait("Lane", "unit")]
    public void The_first_ever_run_starts() =>
        Assert.Equal(MainMoveAction.Start, Decide(Heads(("a", "1")), last: null));

    [Fact, Trait("Lane", "unit")]
    public void The_same_heads_as_the_last_success_are_nothing() =>
        Assert.Equal(MainMoveAction.Nothing, Decide(Heads(("a", "1"), ("b", "2")), Heads(("b", "2"), ("a", "1")), running: false, Ok(10)));

    [Fact, Trait("Lane", "unit")]
    public void A_moved_head_starts() =>
        Assert.Equal(MainMoveAction.Start, Decide(Heads(("a", "1"), ("b", "3")), Heads(("a", "1"), ("b", "2"))));

    [Fact, Trait("Lane", "unit")]
    public void A_repo_added_to_the_map_starts() =>
        Assert.Equal(MainMoveAction.Start, Decide(Heads(("a", "1"), ("b", "2")), Heads(("a", "1"))));

    [Fact, Trait("Lane", "unit")]
    public void A_repo_removed_does_not_start_by_itself() =>
        Assert.Equal(MainMoveAction.Nothing, Decide(Heads(("a", "1")), Heads(("a", "1"), ("b", "2"))));

    [Fact, Trait("Lane", "unit")]
    public void A_repo_that_left_trunk_does_not_start_by_itself_and_one_that_came_back_moved_does() =>
        Assert.Equal((MainMoveAction.Nothing, MainMoveAction.Start), (Decide(Heads(("a", "1")), Heads(("a", "1"), ("b", "2"))), Decide(Heads(("a", "1"), ("b", "9")), Heads(("a", "1"), ("b", "2")))));

    [Theory, Trait("Lane", "unit")]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(5, 60)]
    [InlineData(9, 60)]
    public void Failures_since_the_last_success_back_off_5_10_20_40_then_60_minutes(int failures, int minutes)
    {
        var history = Enumerable.Range(0, failures).Select(i => Failed(minutes - 1 + i)).ToArray();
        var due = Enumerable.Range(0, failures).Select(i => Failed(minutes + i)).ToArray();

        Assert.Equal(MainMoveAction.BackoffNotDue, Decide(Heads(("a", "2")), Heads(("a", "1")), running: false, history));
        Assert.Equal(MainMoveAction.Start, Decide(Heads(("a", "2")), Heads(("a", "1")), running: false, due));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failure_before_a_newer_success_does_not_back_off() =>
        Assert.Equal(MainMoveAction.Start, Decide(Heads(("a", "2")), Heads(("a", "1")), running: false, Ok(1), Failed(2), Failed(3)));

    [Fact, Trait("Lane", "unit")]
    public void The_first_run_failing_backs_off_too() =>
        Assert.Equal(MainMoveAction.BackoffNotDue, Decide(Heads(("a", "1")), last: null, running: false, Failed(1)));

    [Fact, Trait("Lane", "unit")]
    public void Unmoved_heads_stay_nothing_whatever_the_failures() =>
        Assert.Equal(MainMoveAction.Nothing, Decide(Heads(("a", "1")), Heads(("a", "1")), running: false, Failed(1)));

    [Fact, Trait("Lane", "unit")]
    public void Failures_in_a_row_count_from_the_newest_until_a_non_failure()
    {
        Assert.Equal(0, MainMoveDecision.FailedInARow([]));
        Assert.Equal(0, MainMoveDecision.FailedInARow([Ok(1), Failed(2)]));
        Assert.Equal(3, MainMoveDecision.FailedInARow([Failed(1), Failed(2), Failed(3), Ok(4), Failed(5)]));
    }
}
