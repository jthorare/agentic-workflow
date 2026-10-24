using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using static Aw.Sindri.Tests.Workflow.Engine.LoopFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A state's budget is each round's allowance (spec §3.7, W7): spend counts by (state, loop round), a round that overspends
/// does not starve the next one, the recipe's budget stays the cap over the instance, a grant answers its round and may
/// be any size (owner q93), and an instance that began before the rule replays as it ran (pooled).
/// </summary>
public sealed class BudgetPerRoundTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_round_that_overspends_does_not_block_the_next_round()
    {
        var round1 = In("produce", new LoopState(1, 3, Last: null), TokenSpend.PerRound);

        var applied = Assert.IsType<Applied>(Decide(round1, Report(tokens: 250_000)));
        var after = applied.Changes[0].After;
        Assert.Equal(("checks", null), (after.Frame.State, after.Park));

        var round2 = after with { Frame = after.Frame with { State = "produce" }, Loops = new Dictionary<string, LoopState>(StringComparer.Ordinal) { [Revisions] = new(2, 3, Last: null) } };
        Assert.Null(BudgetRules.Exhausted(round2, StepRecipe));
        Assert.Equal(150_000, BudgetRules.Left(round2, StepRecipe));   // round 2's own 200k is untouched; the recipe's 400k has 150k left
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_round_gets_its_own_allowance_and_parks_at_its_own_cap()
    {
        var spent = TokenSpend.PerRound;
        var round1 = In("produce", new LoopState(1, 3, Last: null), spent);
        var round2 = In("produce", new LoopState(2, 3, Last: null), BudgetRules.Spend(round1, StepRecipe, 150_000).Spent);

        Assert.Equal(200_000, BudgetRules.Left(round2, StepRecipe));
        var partway = BudgetRules.Spend(round2, StepRecipe, 150_000);
        Assert.Null(partway.Park);
        Assert.Equal(50_000, BudgetRules.Left(round2 with { Spent = partway.Spent }, StepRecipe));

        var last = BudgetRules.Spend(round2 with { Spent = partway.Spent }, StepRecipe, 50_000);
        Assert.Equal("produce used 200k of 200k tokens (round 2)", last.Park?.Question);
        Assert.Equal(350_000, last.Spent.ByState["produce"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_overrun_is_recorded_on_the_round_for_status()
    {
        var verdict = BudgetRules.Spend(In("produce", new LoopState(1, 3, Last: null), TokenSpend.PerRound), StepRecipe, 250_000);

        Assert.Equal(new RoundOverrun("produce", 1, 250_000, 200_000), Assert.Single(verdict.Spent.Overruns!));
        Assert.Empty(BudgetRules.Spend(In("produce", new LoopState(1, 3, Last: null), TokenSpend.PerRound), StepRecipe, 150_000).Spent.Overruns!);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_recipe_budget_still_caps_the_whole_instance()
    {
        var spent = TokenSpend.PerRound with { Total = 390_000, ByState = new Dictionary<string, int>(StringComparer.Ordinal) { ["produce"] = 390_000 } };
        var round3 = In("produce", new LoopState(3, 3, Last: null), spent);

        var verdict = BudgetRules.Spend(round3, StepRecipe, 15_000);

        Assert.Equal("the instance used 405k of 400k tokens", verdict.Park?.Question);
        Assert.Equal(10_000, BudgetRules.Left(round3, StepRecipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_grant_answers_its_round_and_reaches_any_size()
    {
        var spent = BudgetRules.Spend(In("produce", new LoopState(1, 3, Last: null), TokenSpend.PerRound), StepRecipe, 200_000);
        var parked = Parked(In("produce", new LoopState(1, 3, Last: null), spent.Spent), ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun);

        Assert.Equal("SND-WF-003", BudgetRules.GrantTokens(parked, StepRecipe, 0).Error.Code);
        Assert.Equal(800_001, BudgetRules.GrantTokens(parked, StepRecipe, 800_001).Value.Spent.Granted);   // owner q93: above 4x

        var granted = BudgetRules.GrantTokens(parked, StepRecipe, 150_000).Value;
        Assert.Equal(150_000, granted.Spent.Granted);
        Assert.Equal(150_000, BudgetRules.Left(granted, StepRecipe));   // this round: 200k + 150k - 200k

        var nextRound = granted with { Loops = new Dictionary<string, LoopState>(StringComparer.Ordinal) { [Revisions] = new(2, 3, Last: null) } };
        Assert.Equal(200_000, BudgetRules.Left(nextRound, StepRecipe));   // the grant does not carry over
    }

    [Fact, Trait("Lane", "unit")]
    public void An_instance_from_before_the_rule_keeps_the_pooled_allowance()
    {
        var pooled = In("produce", new LoopState(2, 3, Last: null), Spent(250_000, 250_000));

        Assert.False(pooled.Spent.IsPerRound);
        Assert.Equal("produce used 250k of 200k tokens", BudgetRules.Exhausted(pooled, StepRecipe));
        Assert.Equal(0, BudgetRules.Left(pooled, StepRecipe));
        var parked = Parked(In("produce", spent: Spent(200_000, 200_000)), ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun);
        Assert.True(BudgetRules.GrantTokens(parked, StepRecipe, 200_001).IsSuccess);   // owner q93: no bound by the original budget
    }

    private static Decision Decide(WorkflowInstance instance, Trigger trigger) =>
        TransitionDecision.Decide(new DecisionInput(instance, StepRecipe, trigger, GateAnswers.None, EngineFixture.Context()));

    private static StepCompleted Report(int tokens) =>
        EngineFixture.Completed(3, "done", ("fact:output", "the draft")) with { Tokens = tokens, Via = StepRuntime.Agent };
}
