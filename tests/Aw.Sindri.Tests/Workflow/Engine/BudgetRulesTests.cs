using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using static Aw.Sindri.Tests.Workflow.Engine.LoopFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// Budgets (spec §3.7, §3.8, §5.5, §5.8, W7): every Step report spends against the state's and the recipe's budget;
/// running out parks with budget (kind step, resume rerun); '{budget.tokens.left}' is the lower of the two, grants
/// included; '--grant-tokens n' is any n of at least 1 (owner q93) and fits a budget park once.
/// </summary>
public sealed class BudgetRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Spend_adds_the_run_to_the_state_and_the_instance()
    {
        var verdict = BudgetRules.Spend(In("produce", spent: Spent(120_000, 90_000)), StepRecipe, 30_000);

        Assert.Null(verdict.Park);
        Assert.Equal(150_000, verdict.Spent.Total);
        Assert.Equal(120_000, verdict.Spent.ByState["produce"]);
        Assert.Equal(0, verdict.Spent.Granted);
    }

    [Fact, Trait("Lane", "unit")]
    public void Spend_in_a_state_without_a_budget_counts_against_the_recipe_only()
    {
        var verdict = BudgetRules.Spend(In("checks", spent: Spent(100_000, 100_000)), StepRecipe, 20_000);

        Assert.Null(verdict.Park);
        Assert.Equal(120_000, verdict.Spent.Total);
        Assert.Equal(20_000, verdict.Spent.ByState["checks"]);
        Assert.Equal(100_000, verdict.Spent.ByState["produce"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Budget_refusal_parks_budget()
    {
        var verdict = BudgetRules.Spend(In("produce", spent: Spent(150_000, 150_000)), StepRecipe, 50_000);

        var park = verdict.Park!; // why: the state's 200k is spent; Assert.Equal below fails first if not
        Assert.Equal(SuspensionKind.Park, park.Kind);
        Assert.Equal(ParkReason.Budget, park.Reason);
        Assert.Equal(ParkKind.Step, park.ParkKind);
        Assert.Equal(ResumeKind.Rerun, park.Resume.Kind);
        Assert.Equal("produce used 200k of 200k tokens", park.Question);
        Assert.Equal(200_000, verdict.Spent.ByState["produce"]);
        Assert.Equal(0, BudgetRules.Left(In("produce", spent: verdict.Spent), StepRecipe));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("produce", 150_000, 150_000, 49_999, false)]
    [InlineData("produce", 150_000, 150_000, 50_000, true)]
    [InlineData("checks", 390_000, 150_000, 9_999, false)]
    [InlineData("checks", 390_000, 150_000, 10_000, true)]
    public void A_budget_parks_exactly_at_its_cap(string state, int total, int produce, int tokens, bool parks)
    {
        var verdict = BudgetRules.Spend(In(state, spent: Spent(total, produce)), StepRecipe, tokens);

        Assert.Equal(parks, verdict.Park is not null);
    }

    [Fact, Trait("Lane", "unit")]
    public void Running_out_of_the_recipe_budget_parks_budget()
    {
        var verdict = BudgetRules.Spend(In("checks", spent: Spent(390_000, 150_000)), StepRecipe, 15_000);

        Assert.Equal(ParkReason.Budget, verdict.Park?.Reason);
        Assert.Equal("the instance used 405k of 400k tokens", verdict.Park?.Question);
    }

    [Fact, Trait("Lane", "unit")]
    public void Left_is_the_lower_of_the_state_and_the_recipe_budget_grants_included()
    {
        Assert.Equal(200_000, BudgetRules.Left(In("produce"), StepRecipe));
        Assert.Equal(50_000, BudgetRules.Left(In("produce", spent: Spent(150_000, 150_000)), StepRecipe));
        Assert.Equal(100_000, BudgetRules.Left(In("checks", spent: Spent(300_000, 150_000)), StepRecipe));
        Assert.Equal(150_000, BudgetRules.Left(In("produce", spent: Spent(200_000, 200_000, granted: 150_000)), StepRecipe));
        Assert.Equal(0, BudgetRules.Left(In("checks", spent: Spent(450_000, 150_000)), StepRecipe));
        Assert.Null(BudgetRules.Left(In("produce", spent: Spent(900_000, 900_000)), Unbudgeted));
    }

    [Fact, Trait("Lane", "unit")]
    public void Left_while_parked_reads_the_suspended_recipe_state()
    {
        var parked = Parked(In("produce", spent: Spent(200_000, 200_000)), ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun);

        Assert.Equal(0, BudgetRules.Left(parked, StepRecipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void Token_grant_once_per_park()
    {
        var parked = Parked(In("produce", spent: Spent(200_000, 200_000)), ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun);

        var granted = BudgetRules.GrantTokens(parked, StepRecipe, 100_000).Value;

        Assert.Equal(100_000, granted.Spent.Granted);
        Assert.Equal(100_000, BudgetRules.Left(granted, StepRecipe));
        Assert.Equal(parked.Suspension, granted.Suspension);
        Assert.True(BudgetRules.GrantTokens(parked, StepRecipe, 200_001).IsSuccess);   // owner q93: no upper bound
        Assert.Equal("SND-WF-003", BudgetRules.GrantTokens(parked, StepRecipe, 0).Error.Code);
        Assert.True(BudgetRules.GrantTokens(parked, StepRecipe, 200_000).IsSuccess);

        // The answer that grants pops the park, so the same park can't be granted again; a new park can.
        var resumed = Popped(granted);
        Assert.Equal("SND-WF-003", BudgetRules.GrantTokens(resumed, StepRecipe, 100_000).Error.Code);
        var spentAgain = BudgetRules.Spend(resumed, StepRecipe, 100_000);
        Assert.Equal(ParkReason.Budget, spentAgain.Park?.Reason);
        var parkedAgain = Parked(resumed with { Spent = spentAgain.Spent }, ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun);
        Assert.Equal(200_000, BudgetRules.GrantTokens(parkedAgain, StepRecipe, 100_000).Value.Spent.Granted);
    }

    [Fact, Trait("Lane", "unit")]
    public void Token_grant_on_a_state_without_a_budget_has_no_upper_bound()
    {
        var parked = Parked(In("checks", spent: Spent(400_000, 150_000)), ParkReason.Budget, ParkKind.Step, ResumeKind.Rerun);

        Assert.True(BudgetRules.GrantTokens(parked, StepRecipe, 400_000).IsSuccess);
        Assert.True(BudgetRules.GrantTokens(parked, StepRecipe, 400_001).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void Token_grant_fits_only_a_budget_park()
    {
        var spent = Spent(200_000, 200_000);

        Assert.Equal("SND-WF-003", BudgetRules.GrantTokens(In("produce", spent: spent), StepRecipe, 1_000).Error.Code);
        var rounds = BudgetRules.GrantTokens(Parked(In("produce", spent: spent), ParkReason.Rounds, ParkKind.Refusal, ResumeKind.Rerun), StepRecipe, 1_000);
        Assert.Equal("SND-WF-003", rounds.Error.Code);
        Assert.Contains("--grant-tokens answers a budget park", rounds.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Token_grant_without_a_recipe_budget_has_no_recipe_bound()
    {
        // A 'gate.budget.*' refusal on a recipe with no budget of its own (workflow.item): the bound is the profile's
        // budget, which the caller checks; the rule still needs n >= 1 and a budget park.
        var parked = Parked(In("checks"), ParkReason.Budget, ParkKind.Wait, ResumeKind.Reevaluate);

        Assert.Equal(1_000_000, BudgetRules.GrantTokens(parked, Unbudgeted, 1_000_000).Value.Spent.Granted);
        Assert.Equal("SND-WF-003", BudgetRules.GrantTokens(parked, Unbudgeted, 0).Error.Code);
    }
}
