using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>SND-MODEL-002 (model-pick spec 8, 12): a state's floor is cheap, standard or strong.</summary>
public sealed class ModelRulesTests
{
    private static Recipe Base(StateModel? model) =>
        Of("produce", [("produce", Active("step.produce") with { Model = model }), ("done", Terminal())], [Go("produce", "done", when: Outcome("done"))]);

    private static Located At(params (string Path, int Line, int Column)[] positions) => new("r.yaml", positions);

    [Fact, Trait("Lane", "unit")]
    public void A_recipe_with_no_floor_has_no_findings() => Assert.Empty(CheckModel(Base(model: null)));

    [Theory, Trait("Lane", "unit")]
    [InlineData("cheap")]
    [InlineData("standard")]
    [InlineData("strong")]
    public void A_floor_that_names_a_tier_passes(string floor) => Assert.Empty(CheckModel(Base(new StateModel(floor))));

    [Fact, Trait("Lane", "unit")]
    public void A_state_with_a_model_key_and_no_floor_has_no_findings() => Assert.Empty(CheckModel(Base(new StateModel(Floor: null))));

    [Theory, Trait("Lane", "unit")]
    [InlineData("best")]
    [InlineData("claude-opus-5-5")]
    [InlineData("Strong")]
    [InlineData("")]
    public void A_floor_that_is_not_a_tier_is_SND_MODEL_002(string floor)
    {
        var line = Assert.Single(CheckModel(Base(new StateModel(floor)), At(("states.produce.model.floor", 8, 14))).Lines());

        Assert.Equal(
            $"SND-MODEL-002: the floor of state 'produce' is '{floor}', not a tier at r.yaml:8:14 (states.produce.model.floor); use cheap, standard or strong (a tier, never a model id)",
            line);
    }
}
