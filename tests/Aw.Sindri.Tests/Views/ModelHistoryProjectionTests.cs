using Aw.Kernel.Testing;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'model-history' view fold (model-pick spec §7.2): a launch's model joined to how its Step run ended, one row per (recipe, version, state, model).</summary>
[Lane(TestLane.Unit)]
public sealed class ModelHistoryProjectionTests
{
    private const string Sonnet = "claude-sonnet-5-5";
    private const string Opus = "claude-opus-5-5";
    private readonly ViewsHarness _v = new();

    [Fact]
    public async Task A_completed_run_counts_one_ok_run_with_the_time_it_ended()
    {
        await _v.Begin();
        await _v.Launched(ModelHistoryFolds.Run(1), Sonnet);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(1));

        var row = (await _v.Row("draft", Sonnet))!;

        Expect.Equal((1, 0, 0, 0, 0), (row.Runs, row.Refused, row.CheckFailed, row.Crashed, row.RedraftCaused));
        Expect.Sequence([ModelOutcome.Ok], row.Recent);
        Assert.Equal(_v.Slice.Clock.GetUtcNow(), row.LastRunAt);
        Assert.Equal((ModelHistoryFolds.Recipe, ModelHistoryFolds.Version), (row.Recipe, row.Version));
    }

    [Theory]
    [InlineData("invalid-result: the report is not json", ModelOutcome.Refused)]
    [InlineData("exit", ModelOutcome.Crashed)]
    [InlineData("lost", ModelOutcome.Crashed)]
    [InlineData("idle", ModelOutcome.Crashed)]
    [InlineData("launch", ModelOutcome.Crashed)]
    [InlineData("tests-failed", ModelOutcome.CheckFailed)]
    public async Task A_failed_run_counts_by_the_kind_of_its_reason(string reason, ModelOutcome expected)
    {
        await _v.Begin();
        await _v.Launched(ModelHistoryFolds.Run(1), Sonnet);
        await _v.Failed("draft", ModelHistoryFolds.Run(1), reason);

        var row = (await _v.Row("draft", Sonnet))!;

        Expect.Equal(1, row.Runs);
        Expect.Sequence([expected], row.Recent);
        Expect.Equal((expected == ModelOutcome.Refused ? 1 : 0, expected == ModelOutcome.CheckFailed ? 1 : 0, expected == ModelOutcome.Crashed ? 1 : 0), (row.Refused, row.CheckFailed, row.Crashed));
    }

    [Theory]
    [InlineData("refused", "tests-failed", ModelOutcome.Refused)]
    [InlineData("check_failed", "invalid-result: x", ModelOutcome.CheckFailed)]
    [InlineData("crashed", "tests-failed", ModelOutcome.Crashed)]
    public async Task A_failure_kind_the_engine_recorded_is_used_as_it_is_whatever_the_reason_says(string kind, string reason, ModelOutcome expected)
    {
        await _v.Begin();
        await _v.Launched(ModelHistoryFolds.Run(1), Sonnet);
        await _v.Failed("draft", ModelHistoryFolds.Run(1), reason, kind);

        Expect.Sequence([expected], (await _v.Row("draft", Sonnet))!.Recent);
    }

    [Fact]
    public async Task A_second_launch_of_the_same_run_owns_the_outcome()
    {
        await _v.Begin();
        await _v.Launched(ModelHistoryFolds.Run(1), Sonnet);
        await _v.Launched(ModelHistoryFolds.Run(1), Opus);   // the escalated retry of the same run
        await _v.Failed("draft", ModelHistoryFolds.Run(1), "tests-failed");

        Assert.Null(await _v.Row("draft", Sonnet));
        Expect.Equal(1, (await _v.Row("draft", Opus))!.CheckFailed);
    }

    [Fact]
    public async Task A_launch_with_no_instance_makes_no_row()
    {
        await _v.Begin();
        await _v.Fold(Core.Events.EventTypes.LaunchStarted, ModelHistoryFolds.LaunchPayload(ModelHistoryFolds.Run(1), "draft", Sonnet) with { Instance = null }, A.FirstItemId);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(1));

        Assert.Null(await _v.Row("draft", Sonnet));
    }

    /// <summary>#1481: a real mirrored row names the instance by its address; the fold looked the instance up by the workflow id in some places and the address in others, so a real escalated run wrote no row.</summary>
    [Fact]
    public async Task A_real_escalated_run_counts_the_refused_first_model_and_the_sonnet_ok_run()
    {
        await _v.Begin("write");
        await _v.Launched(ModelHistoryFolds.Run(1), Sonnet, "write");
        await _v.Picked("write", from: Sonnet, "refused");
        await _v.Launched(ModelHistoryFolds.Run(1), Opus, "write");
        await _v.Completed("write", "done", ModelHistoryFolds.Run(1));

        Expect.Sequence([ModelOutcome.Refused], (await _v.Row("write", Sonnet))!.Recent);
        Expect.Sequence([ModelOutcome.Ok], (await _v.Row("write", Opus))!.Recent);
    }

    [Theory]
    [InlineData("refused", ModelOutcome.Refused)]
    [InlineData("check_failed", ModelOutcome.CheckFailed)]
    public async Task An_escalation_counts_the_run_it_escalated_from_for_that_model(string kind, ModelOutcome expected)
    {
        await _v.Begin();
        await _v.Picked("draft", from: Sonnet, kind);

        var row = (await _v.Row("draft", Sonnet))!;

        Expect.Equal(1, row.Runs);
        Expect.Sequence([expected], row.Recent);
    }

    [Fact]
    public async Task A_pick_that_is_not_an_escalation_or_names_no_kind_changes_nothing()
    {
        await _v.Begin();
        await _v.Picked("draft", from: null, kind: null);
        await _v.Picked("draft", from: Sonnet, kind: null);
        await _v.Picked("draft", from: Sonnet, kind: "crashed");

        Assert.Empty((await _v.Views.List<ModelHistoryRow>(Core.Views.ViewNames.ModelHistory, TestContext.Current.CancellationToken)).Value);
    }

    [Fact]
    public async Task Each_model_and_each_state_has_its_own_row()
    {
        await _v.Begin();
        await _v.Launched(ModelHistoryFolds.Run(1), Sonnet);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(1));
        await _v.Launched(ModelHistoryFolds.Run(2), Opus, "check");
        await _v.Failed("check", ModelHistoryFolds.Run(2), "tests-failed");

        Expect.Equal((1, 1), ((await _v.Row("draft", Sonnet))!.Runs, (await _v.Row("check", Opus))!.CheckFailed));
        Assert.Null(await _v.Row("check", Sonnet));
        Assert.Null(await _v.Row("draft", Opus));
    }

    [Fact]
    public async Task A_run_with_no_launch_or_a_launch_that_names_no_model_makes_no_row()
    {
        await _v.Begin();
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(1));   // a desk or block run: no launch at all
        await _v.Launched(ModelHistoryFolds.Run(2), model: null, "check");
        await _v.Completed("check", "apply", ModelHistoryFolds.Run(2));

        Assert.Empty((await _v.Views.List<ModelHistoryRow>(Core.Views.ViewNames.ModelHistory, TestContext.Current.CancellationToken)).Value);
    }

    [Fact]
    public async Task A_recipe_registered_after_its_instance_started_cannot_be_told_its_version_so_makes_no_row()
    {
        await _v.Begin(register: false);
        await _v.Launched(ModelHistoryFolds.Run(1), Sonnet);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(1));

        Assert.Empty((await _v.Views.List<ModelHistoryRow>(Core.Views.ViewNames.ModelHistory, TestContext.Current.CancellationToken)).Value);
    }

    [Fact]
    public async Task A_transition_back_into_a_state_whose_run_completed_charges_a_redraft_to_that_runs_model()
    {
        await _v.Begin();
        await _v.Launched(ModelHistoryFolds.Run(1), Opus);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(1));
        await _v.Launched(ModelHistoryFolds.Run(2), Sonnet, "check");
        await _v.Completed("check", "draft", ModelHistoryFolds.Run(2));   // the 'gaps' transition

        var draft = (await _v.Row("draft", Opus))!;
        var check = (await _v.Row("check", Sonnet))!;

        Expect.Equal((1, 1), (draft.Runs, draft.RedraftCaused));
        Expect.Sequence([ModelOutcome.Redraft], draft.Recent);
        Expect.Equal((1, 0), (check.Runs, check.RedraftCaused));   // the check did its job
        Expect.Sequence([ModelOutcome.Ok], check.Recent);
    }

    [Fact]
    public async Task A_redraft_is_charged_once_per_completed_run()
    {
        await _v.Begin();
        await _v.Launched(ModelHistoryFolds.Run(1), Opus);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(1));
        await _v.Completed("check", "draft", ModelHistoryFolds.Run(2));   // back to draft: charged
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(3));   // a desk redraft: no launch, so no run to count
        await _v.Completed("check", "draft", ModelHistoryFolds.Run(4));   // back again, but no completed run of the model's is left to blame

        Expect.Equal((1, 1), ((await _v.Row("draft", Opus))!.Runs, (await _v.Row("draft", Opus))!.RedraftCaused));
    }

    [Fact]
    public async Task Re_entering_a_state_going_forward_charges_nobody_a_redraft()
    {
        await _v.Begin();
        await _v.Launched(ModelHistoryFolds.Run(1), Opus);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(1));
        await _v.Launched(ModelHistoryFolds.Run(2), Sonnet, "check");
        await _v.Completed("check", "draft", ModelHistoryFolds.Run(2));   // back: the draft is charged
        await _v.Launched(ModelHistoryFolds.Run(3), Opus);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(3));   // forward again: the check's work was not rejected

        var draft = (await _v.Row("draft", Opus))!;
        var check = (await _v.Row("check", Sonnet))!;

        Expect.Equal((2, 1), (draft.Runs, draft.RedraftCaused));
        Expect.Sequence([ModelOutcome.Redraft, ModelOutcome.Ok], draft.Recent);
        Expect.Equal((1, 0), (check.Runs, check.RedraftCaused));
        Expect.Sequence([ModelOutcome.Ok], check.Recent);
    }

    [Fact]
    public async Task Two_rounds_of_gaps_charge_each_draft_once_and_the_check_never()
    {
        await _v.Begin();
        await _v.Launched(ModelHistoryFolds.Run(1), Opus);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(1));
        await _v.Launched(ModelHistoryFolds.Run(2), Sonnet, "check");
        await _v.Completed("check", "draft", ModelHistoryFolds.Run(2));
        await _v.Launched(ModelHistoryFolds.Run(3), Opus);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(3));
        await _v.Launched(ModelHistoryFolds.Run(4), Sonnet, "check");
        await _v.Completed("check", "draft", ModelHistoryFolds.Run(4));
        await _v.Launched(ModelHistoryFolds.Run(5), Opus);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(5));

        var draft = (await _v.Row("draft", Opus))!;
        var check = (await _v.Row("check", Sonnet))!;

        Expect.Equal((3, 2), (draft.Runs, draft.RedraftCaused));
        Expect.Sequence([ModelOutcome.Redraft, ModelOutcome.Redraft, ModelOutcome.Ok], draft.Recent);
        Expect.Equal((2, 0), (check.Runs, check.RedraftCaused));
    }

    [Fact]
    public async Task Moving_forward_or_staying_in_a_state_charges_no_redraft()
    {
        await _v.Begin();
        await _v.Launched(ModelHistoryFolds.Run(1), Opus);
        await _v.Completed("draft", "check", ModelHistoryFolds.Run(1));
        await _v.Completed("check", "apply", ModelHistoryFolds.Run(2));
        await _v.Completed("apply", "apply", ModelHistoryFolds.Run(3));

        var row = (await _v.Row("draft", Opus))!;

        Expect.Equal(0, row.RedraftCaused);
        Expect.Sequence([ModelOutcome.Ok], row.Recent);
    }

    [Fact]
    public async Task Only_the_last_twenty_outcomes_are_kept_but_the_counters_are_for_life()
    {
        await _v.Begin();
        for (var entry = 1; entry <= 25; entry++)
        {
            await _v.Launched(ModelHistoryFolds.Run(entry), Sonnet);
            if (entry <= 5)
            {
                await _v.Failed("draft", ModelHistoryFolds.Run(entry), "tests-failed");
            }
            else
            {
                await _v.Completed("draft", "draft", ModelHistoryFolds.Run(entry));
            }
        }

        var row = (await _v.Row("draft", Sonnet))!;

        Expect.Equal((25, 5, 20, 0), (row.Runs, row.CheckFailed, row.Recent.Count, row.Recent.Count(static o => o != ModelOutcome.Ok)));   // the five old failures have faded
        Assert.Equal(0, row.EscalationRate);
    }
}
