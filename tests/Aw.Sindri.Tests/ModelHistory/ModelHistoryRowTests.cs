using Aw.Kernel.Testing;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ModelHistory;

/// <summary>The pure decisions on a model's history row: its escalation rate, when it is unusual, and how a run's end changes it.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelHistoryRowTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static ModelHistoryRow Row(params ModelOutcome[] recent) =>
        ModelHistoryRow.Empty(RecipeId.From("workflow.scoping"), 3, "draft", "claude-sonnet-5-5") with { Recent = recent };

    private static ModelOutcome Ok => ModelOutcome.Ok;

    private static ModelOutcome Refused => ModelOutcome.Refused;

    private static ModelOutcome Failed => ModelOutcome.CheckFailed;

    private static ModelOutcome Crashed => ModelOutcome.Crashed;

    private static ModelOutcome Redraft => ModelOutcome.Redraft;

    public static TheoryData<ModelOutcome[], double, bool> Rates() => new()
    {
        { [], 0, false },                                  // nothing to count
        { [Crashed, Crashed], 0, false },                  // crashes are not the model's: nothing counted either
        { [Ok], 0, false },
        { [Refused], 1, true },                            // one counted run, all of it failed
        { [Failed, Ok], 0.5, false },                      // exactly half is not more than half
        { [Failed, Refused, Ok], 2.0 / 3, true },
        { [Failed, Crashed, Crashed, Ok], 0.5, false },    // the crashes leave the total: 1 of 2, not 1 of 4
        { [Failed, Crashed, Crashed], 1, true },
        { [Redraft, Redraft, Ok], 0, false },              // a redraft is not a failure of the run
        { [Redraft, Failed], 0.5, false },                 // but it stays in the total
        { [Redraft, Failed, Failed], 2.0 / 3, true },
    };

    [Theory]
    [MemberData(nameof(Rates))]
    public void The_escalation_rate_counts_refused_and_failed_checks_over_the_runs_that_are_not_crashes(ModelOutcome[] recent, double rate, bool unusual)
    {
        var row = Row(recent);

        Assert.Equal(rate, row.EscalationRate, precision: 9);
        Expect.Equal(unusual, row.Unusual);
    }

    [Fact]
    public void A_run_adds_to_its_counter_and_to_the_recent_outcomes_and_sets_the_time()
    {
        var row = Row().After(ModelOutcome.Ok, _at).After(ModelOutcome.Refused, _at.AddMinutes(1)).After(ModelOutcome.CheckFailed, _at.AddMinutes(2)).After(ModelOutcome.Crashed, _at.AddMinutes(3));

        Expect.Equal((4, 1, 1, 1, 0), (row.Runs, row.Refused, row.CheckFailed, row.Crashed, row.RedraftCaused));
        Expect.Sequence([ModelOutcome.Ok, ModelOutcome.Refused, ModelOutcome.CheckFailed, ModelOutcome.Crashed], row.Recent);
        Expect.Equal(_at.AddMinutes(3), row.LastRunAt);
    }

    [Fact]
    public void The_recent_outcomes_keep_the_last_twenty_oldest_first_while_the_counters_keep_everything()
    {
        var row = Row();
        for (var run = 0; run < 25; run++)
        {
            row = row.After(run < 5 ? ModelOutcome.CheckFailed : ModelOutcome.Ok, _at);
        }

        Expect.Equal((25, 5, ModelHistoryRow.Window), (row.Runs, row.CheckFailed, row.Recent.Count));
        Expect.Sequence(Enumerable.Repeat(ModelOutcome.Ok, 20), row.Recent);
    }

    [Fact]
    public void A_redraft_turns_the_latest_ok_into_a_redraft_and_is_not_a_new_run()
    {
        var row = Row(Ok, Ok, Failed, Ok, Failed) with { Runs = 5, CheckFailed = 2, LastRunAt = _at };

        var after = row.Redrafted();

        Expect.Equal((5, 1), (after.Runs, after.RedraftCaused));
        Expect.Sequence([Ok, Ok, Failed, Redraft, Failed], after.Recent);
        Expect.Equal(_at, after.LastRunAt);
    }

    [Fact]
    public void A_redraft_of_a_run_that_has_left_the_window_still_counts()
    {
        var row = Row(Failed, Failed) with { Runs = 40 };

        var after = row.Redrafted();

        Expect.Equal(1, after.RedraftCaused);
        Expect.Sequence([Failed, Failed], after.Recent);
    }
}
