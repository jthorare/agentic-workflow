using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>§12's first recipes pass the wiring and block rules against the shipped blocks and events.</summary>
public sealed class FirstRecipesTests
{
    private static readonly InMemoryChildRecipes _children = new InMemoryChildRecipes().With(Child(FirstRecipes.Step));

    [Fact, Trait("Lane", "unit")]
    public void Workflow_step_raises_nothing()
    {
        var located = Located.Nowhere with { Catalog = WiringCatalog.Shipped };

        Assert.Empty(CheckWiring(FirstRecipes.Step, located));
        Assert.Empty(CheckBlocks(FirstRecipes.Step, located));
    }

    [Fact, Trait("Lane", "unit")]
    public void Workflow_item_raises_no_error_only_its_slot_and_unchecked_effect()
    {
        var located = Located.Nowhere with { Catalog = WiringCatalog.Shipped, Children = _children };

        Assert.Empty(CheckWiring(FirstRecipes.Item, located));
        Assert.Equal(
            [
                "RCP-EFFECT-001: effect 'effect.claim.ref' wasn't checked against an effect allowlist, since the profile has no workflow.effects at r.yaml:1:1 (transitions[2].effects[0].use); add workflow.effects to the profile; until then the effect is refused at run time",
                "RCP-RES-001: resource 'slot' has no allocator in this build at r.yaml:1:1 (states.startable.waitFor.resource); it parks with SND-WF-010 until the session-slot stub lands (P7)",
            ],
            CheckBlocks(FirstRecipes.Item, located).Lines());
        Assert.True(RecipeValidator.Passes(CheckBlocks(FirstRecipes.Item, located)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Workflow_item_with_an_allowlist_that_has_its_effect_raises_only_the_slot()
    {
        var profile = new RecipeProfileFacts(new HashSet<BlockId> { BlockId.From("effect.claim.ref") });
        var located = Located.Nowhere with { Catalog = WiringCatalog.Shipped, Children = _children, Profile = profile };

        var finding = Assert.Single(CheckBlocks(FirstRecipes.Item, located));

        Assert.Equal("RCP-RES-001", finding.Rule);
        Assert.Equal(FindingSeverity.Information, finding.Severity);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_whole_validator_passes_both_recipes()
    {
        var context = new ValidationContext(WiringCatalog.Shipped, RecipeProfileFacts.None, _children, BaseRecipe: null);
        var source = new RecipeSource("workflow.item.yaml", SourcePositions.None);

        Assert.True(RecipeValidator.Passes(RecipeValidator.Validate(new RecipeDocument(FirstRecipes.Item, RecipeHash.Of("{}"u8), source), context)));
        Assert.True(RecipeValidator.Passes(RecipeValidator.Validate(new RecipeDocument(FirstRecipes.Step, RecipeHash.Of("{}"u8), source), context)));
    }
}
