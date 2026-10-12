using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Capabilities.CapabilityRecipe;

namespace Aw.Sindri.Tests.Workflow.Capabilities;

/// <summary>
/// The capability rules on a hand-written recipe, validated with no base (spec §3.2, §3.3, §4.3): RCP-CAP-004 (a
/// hand-written recipe has no 'base') and RCP-CAP-007 (a capability parameter is a literal or '{ profile }').
/// </summary>
public sealed class HandWrittenCapabilityTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_hand_written_recipe_raises_nothing()
    {
        Assert.Empty(Lines(ARecipe.Minimal(RecipeId.From("workflow.plain")), baseRecipe: null));
        Assert.Empty(Lines(Base, baseRecipe: null));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_hand_written_recipe_with_a_base_is_refused()
    {
        // The recipe of invalid/RCP-CAP-004.yaml; 'base' is at 4:1.
        var recipe = ARecipe.Minimal(RecipeId.From("workflow.cap-handwritten")) with
        {
            Base = new RecipeRef(RecipeId.From("workflow.cap-base"), BaseHash),
        };

        Assert.Equal(
            ["RCP-CAP-004: hand-written recipe workflow.cap-handwritten names base workflow.cap-base@5aa84cb58a5a at RCP-CAP-004.yaml:4:1 (base); remove base, which only Sindri's generator writes"],
            Lines(recipe, baseRecipe: null, "RCP-CAP-004.yaml", [("base", 4, 1)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_base_in_the_context_the_rules_that_compare_with_one_do_not_run()
    {
        var recipe = (Generated with { Base = null })
            .WithTransition(2, t => t with { Effects = [Push("upstream"), Use("effect.claim.ref")] });

        Assert.Empty(Lines(recipe, baseRecipe: null));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_capability_parameter_that_reads_a_fact_is_refused()
    {
        // The recipe of invalid/RCP-CAP-007.yaml; 'remote' is at 22:17.
        var recipe = ARecipe.Minimal(RecipeId.From("workflow.cap-fact-parameter"))
            .WithTransition(0, t => t with { Effects = [PushWith("remote", FactValue("fact:remote"))] });

        Assert.Equal(
            ["RCP-CAP-007: capability parameter 'remote' of effect.repo.push reads fact:remote at RCP-CAP-007.yaml:22:17 (transitions[0].effects[0].with.remote); give it a literal or { profile: <key> }, never a fact"],
            Lines(recipe, baseRecipe: null, "RCP-CAP-007.yaml", [("transitions[0].effects[0].with.remote", 22, 17)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_capability_parameter_from_the_profile_and_a_plain_parameter_from_a_fact_are_allowed()
    {
        var push = Push("origin") with
        {
            With = new Dictionary<string, WithValue>(StringComparer.Ordinal)
            {
                ["remote"] = new(Literal: null, Fact: null, Profile: "workflow.remote"),
                ["message"] = FactValue("fact:output"),
            },
        };
        var recipe = ARecipe.Minimal(RecipeId.From("workflow.cap-profile")).WithTransition(0, t => t with { Effects = [push] });

        Assert.Empty(Lines(recipe, baseRecipe: null));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fact_capability_parameter_is_refused_in_a_generated_recipe_too()
    {
        var recipe = Generated.WithTransition(2, t => t with { Effects = [PushWith("remote", FactValue("fact:remote"))] });

        Assert.Contains(
            "RCP-CAP-007: capability parameter 'remote' of effect.repo.push reads fact:remote at r.yaml:1:1 (transitions[2].effects[0].with.remote); give it a literal or { profile: <key> }, never a fact",
            Lines(recipe, Base),
            StringComparer.Ordinal);
    }

    private static WithValue FactValue(string key) => new(Literal: null, FactKey.From(key), Profile: null);

    private static BlockUse PushWith(string name, WithValue value) =>
        Push("origin") with { With = new Dictionary<string, WithValue>(StringComparer.Ordinal) { [name] = value } };
}
