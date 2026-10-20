using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;
using static Aw.Sindri.Tests.GetInstance.StepRunFixtures;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The Step runs folded from an instance's ledger events: start, end, outcome, try, tokens and actor, in start order.</summary>
[Lane(TestLane.Unit)]
public sealed class StepRunsTests
{
    private static readonly IReadOnlyDictionary<string, int> _noCaps = new Dictionary<string, int>(StringComparer.Ordinal);

    private static IReadOnlyList<StepRun> Fold(params RecordedWorkflowEvent[] events) => StepRuns.Fold(events, _noCaps);

    [Fact]
    public void A_run_with_no_report_is_running_with_no_end_duration_outcome_or_actor()
    {
        var run = Assert.Single(Fold(Started("produce", "1.1", minutes: 2)));

        Assert.Equal(new StepRun("1.1", "produce", Attempt: 1, MaxAttempts: null, StepRunState.Running, StepRuntime.Agent, At(2), EndedAt: null, DurationMs: null, Outcome: null, Tokens: null, Actor: null), run);
    }

    [Fact]
    public void A_completed_run_has_its_end_duration_outcome_tokens_and_the_reporting_actor()
    {
        var run = Assert.Single(Fold(Started("produce", "1.1"), Completed("produce", "checks", "1.1", "done", minutes: 5, tokens: 1_200, actor: "agent:session")));

        Assert.Equal(new StepRun("1.1", "produce", Attempt: 1, MaxAttempts: null, StepRunState.Passed, StepRuntime.Agent, At(0), At(5), DurationMs: 300_000, "done", Tokens: 1_200, "agent:session"), run);
    }

    [Fact]
    public void A_failed_run_is_failed_with_no_outcome()
    {
        var run = Assert.Single(Fold(Started("write", "1.1"), Completed("write", "parked", "1.1", outcome: null, kind: "step.failed", tokens: 0)));

        Assert.Equal((StepRunState.Failed, null), (run.State, run.Outcome));
    }

    [Fact]
    public void The_next_Step_a_report_opens_is_the_next_run_and_the_runs_stay_in_start_order()
    {
        var runs = Fold(
            Started("produce", "1.1"),
            Completed("produce", "checks", "1.1", "done", minutes: 4, next: "2.1"),
            Completed("checks", "ship", "2.1", "pass", minutes: 9));

        Assert.Equal([("1.1", "produce", StepRunState.Passed), ("2.1", "checks", StepRunState.Passed)], runs.Select(static r => (r.Token, r.Step, r.State)));
        Assert.Equal(At(4), runs[1].StartedAt);
    }

    [Fact]
    public void A_step_run_again_is_the_next_try_and_takes_its_max_from_the_states_cap()
    {
        var caps = new Dictionary<string, int>(StringComparer.Ordinal) { ["verify"] = 3 };
        var runs = StepRuns.Fold(
            [
                Started("verify", "1.1"),
                Completed("verify", "mitigate", "1.1", "fail", minutes: 3, next: "2.1"),
                Completed("mitigate", "verify", "2.1", "done", minutes: 6, next: "3.1"),
                Completed("verify", "done", "3.1", "pass", minutes: 8),
            ],
            caps);

        Assert.Equal([("verify", 1, 3), ("mitigate", 1, null), ("verify", 2, 3)], runs.Select(static r => (r.Step, r.Attempt, r.MaxAttempts)));
    }

    [Fact]
    public void A_takeover_or_an_escalation_leaves_the_run_running_because_the_frame_is_untouched()
    {
        var run = Assert.Single(Fold(Started("produce", "1.1"), Parked("produce", minutes: 30, kind: "takeover"), Parked("produce", minutes: 40, kind: "escalate")));

        Assert.Equal((StepRunState.Running, null), (run.State, run.EndedAt));
    }

    [Fact]
    public void A_refused_report_marks_its_run_refused_and_the_re_run_opens_beside_it()
    {
        var runs = Fold(
            Started("produce", "1.1"),
            Completed("produce", "produce", "1.1", "done", minutes: 3, next: "1.2", type: "workflow.refused"));

        Assert.Equal([("1.1", StepRunState.Refused), ("1.2", StepRunState.Running)], runs.Select(static r => (r.Token, r.State)));
    }

    [Fact]
    public void A_park_leaves_the_run_open()
    {
        var run = Assert.Single(Fold(Started("produce", "1.1"), Parked("produce", minutes: 30)));

        Assert.Equal(StepRunState.Running, run.State);
    }

    [Fact]
    public void A_cancel_ends_an_open_run_cancelled_with_the_cancelling_actor()
    {
        var run = Assert.Single(Fold(Started("produce", "1.1"), Cancelled("produce", minutes: 7)));

        Assert.Equal((StepRunState.Cancelled, At(7), 420_000L, "human:owner"), (run.State, run.EndedAt, run.DurationMs, run.Actor));
    }

    [Fact]
    public void Leaving_the_entry_with_no_report_abandons_the_run()
    {
        var run = Assert.Single(Fold(Started("produce", "1.1"), Moved("produce", "elsewhere", minutes: 6)));

        Assert.Equal((StepRunState.Abandoned, At(6)), (run.State, run.EndedAt));
    }

    [Fact]
    public void The_fold_is_a_function_of_the_events_so_folding_again_gives_the_same_runs()
    {
        RecordedWorkflowEvent[] events = [Started("produce", "1.1"), Completed("produce", "checks", "1.1", "done", next: "2.1"), Completed("checks", "ship", "2.1", "pass", minutes: 9)];

        Assert.Equal(Fold(events), Fold(events));
    }

    [Fact]
    public void A_report_for_a_run_it_never_saw_open_adds_nothing() =>
        Assert.Empty(Fold(Completed("produce", "checks", "7.1", "done")));
}
