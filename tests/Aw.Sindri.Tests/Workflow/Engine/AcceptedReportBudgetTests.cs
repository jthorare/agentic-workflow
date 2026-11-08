using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.LoopFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A Step's accepted report is never thrown away for its cost: the transition applies and the spend is recorded, and the budget is checked where the next Step would
/// start (spec §3.7; the P8 proof found the rerun discarding a good report and spending another 780k). A failed run still parks on its budget.
/// </summary>
public sealed class AcceptedReportBudgetTests
{
    [Fact, Trait("Lane", "unit")]
    public void An_accepted_report_over_the_state_budget_still_takes_its_transition()
    {
        var instance = In("produce", new LoopState(1, 3, Last: null), Spent(150_000, 150_000));

        var applied = Assert.IsType<Applied>(Decide(instance, Report(tokens: 60_000)));

        var after = applied.Changes[0].After;
        Assert.Equal(("checks", null, 210_000), (after.Frame.State, after.Park, after.Spent.ByState["produce"]));
        Assert.Equal(210_000, after.Spent.Total);
        Assert.Equal(FollowUpKind.StartRun, Assert.Single(applied.FollowUps).Kind);   // 'checks' has its own room (the recipe's 400k holds)
    }

    [Fact, Trait("Lane", "unit")]
    public void An_accepted_report_that_spends_the_instance_budget_parks_where_the_next_step_would_start()
    {
        var instance = In("produce", new LoopState(1, 3, Last: null), Spent(380_000, 100_000));

        var applied = Assert.IsType<Applied>(Decide(instance, Report(tokens: 30_000)));

        var after = applied.Changes[0].After;
        Assert.Equal(("checks", ParkReason.Budget, 410_000), (after.Frame.State, after.Park, after.Spent.Total));
        Assert.Empty(applied.FollowUps);   // the next Step did not start
        Assert.Equal("the instance used 410k of 400k tokens", after.Suspension?.Question);
        Assert.Equal(ResumeKind.Restart, after.Suspension?.Resume.Kind);   // its answer starts that Step; it is not a round of the loop
    }

    [Fact, Trait("Lane", "unit")]
    public void A_grant_on_that_park_starts_the_step_without_counting_a_round()
    {
        var parked = Assert.IsType<Applied>(Decide(In("produce", new LoopState(1, 3, Last: null), Spent(380_000, 100_000)), Report(tokens: 30_000))).Changes[0].After;

        var granted = Assert.IsType<Applied>(Decide(parked, Grant(50_000)));

        var after = granted.Changes[0].After;
        Assert.Equal(("checks", null, 1), (after.Frame.State, after.Park, after.Loops[Revisions].Round));
        Assert.Equal(FollowUpKind.StartRun, Assert.Single(granted.FollowUps).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_report_a_gate_refuses_over_budget_parks_on_the_budget_and_starts_no_run()
    {
        var instance = In("produce", new LoopState(1, 3, Last: null), Spent(150_000, 150_000));
        var gated = new GateUse(BlockId.From("gate.test.check"), Threshold: null, new Dictionary<string, WithValue>(StringComparer.Ordinal));
        var recipe = StepRecipe with { Transitions = [StepRecipe.Transitions[0] with { Gates = [gated] }, .. StepRecipe.Transitions.Skip(1)] };
        var failing = EngineFixture.Gates(("gate.test.check", GateVerdict.Fail, null));

        var refused = Assert.IsType<Refused>(TransitionDecision.Decide(new DecisionInput(instance, recipe, Report(tokens: 60_000), failing, EngineFixture.Context())));

        var after = refused.Changes[0].After;
        Assert.Equal((ParkReason.Budget, "produce"), (after.Park, after.Frame.State));
        Assert.Empty(refused.FollowUps);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_needs_answer_park_answered_over_budget_parks_on_the_budget_and_starts_no_run()
    {
        var parked = Parked(In("produce", new LoopState(1, 3, Last: null), Spent(210_000, 210_000)), ParkReason.NeedsAnswer, ParkKind.Step, ResumeKind.Rerun);
        var text = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), new string('d', 64)), AnswerVariant.Text, "Try again.", Amount: null, Forwarded: null);

        var applied = Assert.IsType<Applied>(Decide(parked, text));

        Assert.Equal(ParkReason.Budget, applied.Changes[0].After.Park);
        Assert.Empty(applied.FollowUps);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_run_over_budget_still_parks_on_its_budget()
    {
        var instance = In("produce", new LoopState(1, 3, Last: null), Spent(150_000, 150_000));
        var failed = new StepFailed("service:desk", RunToken.For(3, 1), "the build broke", Tokens: 60_000, new string('e', 64));

        var applied = Assert.IsType<Applied>(Decide(instance, failed));

        var after = applied.Changes[0].After;
        Assert.Equal((ParkReason.Budget, "produce", 210_000), (after.Park, after.Frame.State, after.Spent.Total));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_history_from_before_the_fix_parks_on_the_report_as_it_did()
    {
        var instance = In("produce", new LoopState(1, 3, Last: null), Spent(150_000, 150_000));

        var applied = Assert.IsType<Applied>(Decide(instance, Report(tokens: 60_000), legacy: true));

        var after = applied.Changes[0].After;
        Assert.Equal((ParkReason.Budget, "produce"), (after.Park, after.Frame.State));
    }

    private static Decision Decide(WorkflowInstance instance, Trigger trigger, bool legacy = false) =>
        TransitionDecision.Decide(new DecisionInput(instance, StepRecipe, trigger, GateAnswers.None, EngineFixture.Context() with { LegacyBudgetPark = legacy }));

    private static StepCompleted Report(int tokens) =>
        EngineFixture.Completed(3, "done", ("fact:output", "the draft")) with { Tokens = tokens, Via = StepRuntime.Agent };

    private static AnswerGiven Grant(int amount) => new(
        "human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), new string('d', 64)), AnswerVariant.GrantTokens, Text: null, amount, Forwarded: null);
}
