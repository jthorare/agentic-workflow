using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Capabilities.CapabilityRecipe;

namespace Aw.Sindri.Tests.Workflow.Capabilities;

/// <summary>
/// RCP-CAP-006 (spec §4.3, W10): each loop, matched by the state it's on, has a cap at or below the base's, and each
/// budget the base sets is present and at or below it. The base caps 'produce' with loop 'revisions' at 3, budgets
/// 'produce' at 200k and the recipe at 400k.
/// </summary>
public sealed class CapabilityLoopAndBudgetTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_loop_cap_above_the_base_is_refused()
    {
        // The recipe of invalid/RCP-CAP-006.yaml; 'cap' is at 52:23.
        var recipe = Generated with
        {
            Id = RecipeId.From("workflow.cap-006"),
            Loops = new Dictionary<string, LoopSpec>(StringComparer.Ordinal) { ["revisions"] = new(5, LoopSpec.DefaultNoProgress) },
        };

        Assert.Equal(
            ["RCP-CAP-006: loop 'revisions' on state 'produce' has cap 5, above base workflow.cap-base's 3 at RCP-CAP-006.yaml:52:23 (loops.revisions.cap); lower cap to 3 or less"],
            Lines(recipe, Base, "RCP-CAP-006.yaml", [("loops.revisions.cap", 52, 23)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Loops_are_matched_by_state_not_by_name()
    {
        var recipe = Generated.WithState("produce", s => s with { Loop = "rounds" }) with
        {
            Loops = new Dictionary<string, LoopSpec>(StringComparer.Ordinal)
            {
                ["revisions"] = new(9, LoopSpec.DefaultNoProgress),
                ["rounds"] = new(3, LoopSpec.DefaultNoProgress),
            },
        };

        Assert.Empty(Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_that_drops_its_loop_is_refused()
    {
        var recipe = Generated.WithState("produce", s => s with { Loop = null });

        Assert.Equal(
            ["RCP-CAP-006: state 'produce' has no loop, but base workflow.cap-base caps it at 3 rounds at r.yaml:1:1 (states.produce); put it in a loop with cap 3 or less"],
            Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_budget_above_the_base_is_refused()
    {
        var recipe = Generated with { Budget = new BudgetSpec(500_000) };

        Assert.Equal(
            ["RCP-CAP-006: recipe budget is 500000 tokens, above base workflow.cap-base's 400000 at r.yaml:1:1 (budget.tokens); lower it to 400000 tokens or less"],
            Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_budget_the_base_sets_cannot_be_left_out()
    {
        var recipe = Generated with { Budget = null };

        Assert.Equal(
            ["RCP-CAP-006: recipe budget is unset, but base workflow.cap-base sets 400000 tokens at r.yaml:1:1 (budget); set it to 400000 tokens or less"],
            Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_budget_above_the_base_or_left_out_is_refused()
    {
        var above = Generated.WithState("produce", s => s with { Budget = new BudgetSpec(250_000) });
        var unset = Generated.WithState("produce", s => s with { Budget = null });

        Assert.Equal(
            ["RCP-CAP-006: state 'produce' budget is 250000 tokens, above base workflow.cap-base's 200000 at r.yaml:1:1 (states.produce.budget.tokens); lower it to 200000 tokens or less"],
            Lines(above, Base));
        Assert.Equal(
            ["RCP-CAP-006: state 'produce' budget is unset, but base workflow.cap-base sets 200000 tokens at r.yaml:1:1 (states.produce.budget); set it to 200000 tokens or less"],
            Lines(unset, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_budget_the_base_does_not_set_may_be_added()
    {
        var recipe = Generated.WithState("publish", s => s with { Budget = new BudgetSpec(50_000) });

        Assert.Empty(Lines(recipe, Base));
    }
}
