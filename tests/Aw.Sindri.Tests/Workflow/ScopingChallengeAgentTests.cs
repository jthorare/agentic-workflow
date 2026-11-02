using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The scoping recipe declares the second opinion's contrast itself (#1269): its challenge state differs from draft, instead of the engine knowing the pair.</summary>
public sealed class ScopingChallengeAgentTests
{
    private static Recipe Scoping() => RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/workflow.scoping.yaml"), "workflow.scoping.yaml").Recipe;

    [Fact, Trait("Lane", "unit")]
    public void The_challenge_state_differs_from_the_draft_state() =>
        Assert.Equal(new StateAgent(DifferentFrom: "draft"), Scoping().States["challenge"].Agent);

    [Fact, Trait("Lane", "unit")]
    public void The_scoping_recipe_is_version_20() => Assert.Equal(20, Scoping().Version);

    [Fact, Trait("Lane", "unit")]
    public void No_other_scoping_state_names_an_agent() =>
        Assert.Empty(Scoping().States.Where(s => s.Key != "challenge" && s.Value.Agent is not null).Select(s => s.Key));

    /// <summary>Model-pick M-9 (spec 8): draft needs a strong model and the second opinion at least a standard one, and neither names a model of its own, so the pick (or the floor, with the pick off) decides.</summary>
    [Fact, Trait("Lane", "unit")]
    public void Draft_has_a_strong_floor_and_challenge_a_standard_one_and_neither_pins_a_model()
    {
        var states = Scoping().States;

        Assert.Equal(("strong", "standard"), (states["draft"].Model?.Floor, states["challenge"].Model?.Floor));
        Assert.False(states["draft"].Step!.With.ContainsKey("model"));
        Assert.False(states["challenge"].Step!.With.ContainsKey("model"));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_other_scoping_state_has_a_floor() =>
        Assert.Empty(Scoping().States.Where(s => s.Key is not ("draft" or "challenge") && s.Value.Model is not null).Select(s => s.Key));
}
