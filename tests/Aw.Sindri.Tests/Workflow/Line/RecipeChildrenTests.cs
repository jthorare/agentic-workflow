using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Graph.GraphRecipe;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>The recipes a recipe starts: its child waits' and its start-phase effects' (line spec LN-WF1), pinned and approved with it.</summary>
public sealed class RecipeChildrenTests
{
    private static RecipeTransition Starting(string recipe, string from = "a", string to = "b") => Go(from, to) with
    {
        Effects =
        [
            new BlockUse(BlockId.From(RecipeChildren.StartPhase), new Dictionary<string, WithValue>(StringComparer.Ordinal)
            {
                ["recipe"] = new WithValue(JsonSerializer.SerializeToElement(recipe), Fact: null, Profile: null),
            }),
        ],
    };

    private static Recipe Single(params RecipeTransition[] transitions) => Of("a", [("a", Active()), ("b", Terminal())], transitions);

    [Fact, Trait("Lane", "unit")]
    public void A_start_phase_effect_names_its_recipe_as_one_the_recipe_starts()
    {
        var recipe = Single(Starting("workflow.phase"));

        Assert.Equal(["workflow.phase"], RecipeChildren.Of(recipe).Select(r => r.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_child_wait_and_a_start_phase_effect_naming_the_same_recipe_count_once()
    {
        var recipe = Of("a", [("a", Active()), ("w", WaitForChild("workflow.phase")), ("b", Terminal())], [Starting("workflow.phase"), Go("w", "b", "wait.satisfied")]);

        Assert.Equal(["workflow.phase"], RecipeChildren.Of(recipe).Select(r => r.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_start_phase_effect_with_no_literal_recipe_names_none()
    {
        var noRecipe = Go("a", "b") with { Effects = [new BlockUse(BlockId.From(RecipeChildren.StartPhase), new Dictionary<string, WithValue>(StringComparer.Ordinal))] };

        Assert.Empty(RecipeChildren.Of(Single(noRecipe)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Another_effect_names_no_recipe()
    {
        var other = Go("a", "b") with
        {
            Effects = [new BlockUse(BlockId.From("effect.notify.desk"), new Dictionary<string, WithValue>(StringComparer.Ordinal)
            {
                ["recipe"] = new WithValue(JsonSerializer.SerializeToElement("workflow.phase"), Fact: null, Profile: null),
            })],
        };

        Assert.Empty(RecipeChildren.Of(Single(other)));
    }
}
