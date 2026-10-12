using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Capabilities.CapabilityRecipe;

namespace Aw.Sindri.Tests.Workflow.Capabilities;

/// <summary>
/// A generated recipe's capability uses against its base (spec §2, §4.3, W10): RCP-CAP-001 (the uses, block plus
/// from -> to, and the children are a subset of the base's) and RCP-CAP-002 (capability parameter values equal the base's).
/// </summary>
public sealed class CapabilityUseTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_generated_recipe_at_or_below_its_base_raises_nothing() =>
        Assert.Empty(Lines(Generated, Base));

    [Fact, Trait("Lane", "unit")]
    public void A_capability_use_the_base_does_not_have_is_refused()
    {
        // The recipe of invalid/RCP-CAP-001.yaml; the short form 'effect.claim.ref' is at 49:9.
        var recipe = (Generated with { Id = RecipeId.From("workflow.cap-001") })
            .WithTransition(2, t => t with { Effects = [Push("origin"), Use("effect.claim.ref")] });

        Assert.Equal(
            ["RCP-CAP-001: capability use effect.claim.ref on publish -> running isn't in base workflow.cap-base at RCP-CAP-001.yaml:49:9 (transitions[2].effects[1].use); remove it, or generate the recipe from a base that has it"],
            Lines(recipe, Base, "RCP-CAP-001.yaml", [("transitions[2].effects[1].use", 49, 9)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_capability_use_moved_to_another_transition_is_refused()
    {
        var recipe = Generated
            .WithTransition(0, t => t with { Effects = [Push("origin")] })
            .WithTransition(2, t => t with { Effects = [] });

        Assert.Equal(
            ["RCP-CAP-001: capability use effect.repo.push on produce -> publish isn't in base workflow.cap-base at r.yaml:1:1 (transitions[0].effects[0].use); remove it, or generate the recipe from a base that has it"],
            Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_the_base_does_not_start_is_refused()
    {
        // why: Generated's running state is a child wait, so WaitFor and Child are set
        var recipe = Generated.WithState("running", s => s with
        {
            WaitFor = s.WaitFor! with { Child = s.WaitFor.Child! with { Recipe = RecipeId.From("workflow.other") } },
        });

        Assert.Equal(
            ["RCP-CAP-001: capability use child workflow.other in state 'running' isn't in base workflow.cap-base at r.yaml:1:1 (states.running.waitFor.child.recipe); remove it, or generate the recipe from a base that has it"],
            Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_use_without_capabilities_may_be_added()
    {
        var recipe = Generated.WithTransition(3, t => t with { Gates = [Gate("gate.check")] });

        Assert.Empty(Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_capability_parameter_that_differs_from_the_base_is_refused()
    {
        // The recipe of invalid/RCP-CAP-002.yaml; 'remote' is at 48:17.
        var recipe = (Generated with { Id = RecipeId.From("workflow.cap-002") })
            .WithTransition(2, t => t with { Effects = [Push("upstream")] });

        Assert.Equal(
            ["RCP-CAP-002: capability parameter 'remote' of effect.repo.push on publish -> running is \"upstream\", but base workflow.cap-base has \"origin\" at RCP-CAP-002.yaml:48:17 (transitions[2].effects[0].with.remote); set it to the base's value"],
            Lines(recipe, Base, "RCP-CAP-002.yaml", [("transitions[2].effects[0].with.remote", 48, 17)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_capability_parameter_left_out_is_refused()
    {
        var recipe = Generated.WithTransition(2, t => t with { Effects = [Use("effect.repo.push")] });

        Assert.Equal(
            ["RCP-CAP-002: capability parameter 'remote' of effect.repo.push on publish -> running is unset, but base workflow.cap-base has \"origin\" at r.yaml:1:1 (transitions[2].effects[0].with.remote); set it to the base's value"],
            Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parameter_that_is_not_a_capability_field_may_differ()
    {
        var push = Push("origin") with
        {
            With = new Dictionary<string, WithValue>(Push("origin").With, StringComparer.Ordinal) { ["message"] = Literal("generated") },
        };
        var recipe = Generated.WithTransition(2, t => t with { Effects = [push] });

        Assert.Empty(Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void Timeouts_are_not_compared()
    {
        var recipe = Generated.WithState("produce", s => s with { Timeout = new TimeoutSpec(RecipeDuration.From("7d"), TimeoutAction.Escalate) });

        Assert.Empty(Lines(recipe, Base));
    }
}
