using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

public sealed class RecipeJsonTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_long_form_round_trips_through_the_package_context()
    {
        var recipe = ARecipe.Minimal(RecipeId.From("workflow.step"));

        var json = AwJson.Serialize(recipe, SliceHarness.Json);
        var back = AwJson.Deserialize<Recipe>(json, SliceHarness.Json).Value;

        Assert.Equal(json, AwJson.Serialize(back, SliceHarness.Json));
        Assert.Contains("\"kind\":\"active\"", System.Text.Encoding.UTF8.GetString(json), StringComparison.Ordinal);
    }
}
