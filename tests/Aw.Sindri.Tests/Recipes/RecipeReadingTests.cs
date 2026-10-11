using Aw.Sindri.Core.Recipes;
using Aw.Sindri.Infrastructure.Recipes;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.Recipes;

/// <summary>Recipe YAML through YamlToJson and AwJson: the schema check, and a hash that ignores comments, key order and state order.</summary>
public sealed class RecipeReadingTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_recipe_reads_into_its_record()
    {
        var (recipe, _) = TestRecipes.Read(TestRecipes.Loop);

        Assert.Equal(("workflow.loop", "work", 3, 5), (recipe.Id.Value, recipe.Initial, recipe.States.Count, recipe.Transitions.Count));
        Assert.Equal((StateKind.Active, TimeoutAction.Retry), (recipe.States["work"].Kind, recipe.States["work"].OnTimeout));
        Assert.Equal(2, recipe.Loops!["fix"].Cap);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("version: 1", "version: 1\ncolour: blue", "'colour' is not a key of a recipe")]
    [InlineData("work: { kind: active,", "work: { kind: busy,", "states.work.kind")]
    [InlineData("id: workflow.loop", "id: Workflow Loop", "recipe id")]
    [InlineData("version: 1", "version: 1\nversion: 2", "line")]
    public void A_recipe_that_does_not_fit_the_schema_is_one_schema_problem(string from, string to, string expected)
    {
        var read = RecipeReading.Read(TestRecipes.Loop.Replace(from, to, StringComparison.Ordinal), SliceHarness.Json);

        Assert.Null(read.Recipe);
        Assert.Equal(RecipeChecks.Schema, read.Problem?.Check);
        Assert.Contains(expected, read.Problem?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_hash_ignores_comments_and_state_order_but_not_content()
    {
        var reordered = TestRecipes.Loop
            .Replace("  work: { kind: active, step: step.checks, timeout: 10m, onTimeout: retry }\n", string.Empty, StringComparison.Ordinal)
            .Replace("  done: { kind: terminal }", "  done: { kind: terminal }\n  work: { onTimeout: retry, kind: active, step: step.checks, timeout: 10m }", StringComparison.Ordinal);

        var (_, original) = TestRecipes.Read(TestRecipes.Loop);
        var (_, commented) = TestRecipes.Read("# a comment\n" + reordered);
        var (_, changed) = TestRecipes.Read(TestRecipes.Loop.Replace("cap: 2", "cap: 3", StringComparison.Ordinal));

        Assert.Equal(original, commented);
        Assert.NotEqual(original, changed);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_hash_ignores_the_description_and_the_next_step_text()
    {
        var worded = TestRecipes.Loop
            .Replace("version: 1", "version: 1\ndescription: a loop for tests", StringComparison.Ordinal)
            .Replace("  review: { kind: wait,", "  review: { next: wait for the review, kind: wait,", StringComparison.Ordinal);

        var (_, original) = TestRecipes.Read(TestRecipes.Loop);
        var (_, reworded) = TestRecipes.Read(worded);

        Assert.Equal(original, reworded);
    }

    // Golden: the built-in recipes' hashes. A change here means every instance started on a built-in is stranded by the
    // upgrade (SND-WF-008), so it is a reviewed change that says why the recipe's content changed.
    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.item", "091c60861918cf4bb6371b7701b29aa60a43eeed57035fc3ad72a43d933d0328")]
    [InlineData("workflow.step", "97fda076a0570797842fd78bdf26b293ae5195a191e8000ee812f7a62ce87e2a")]
    public void The_built_in_recipes_keep_their_hashes(string name, string hash) =>
        Assert.Equal(hash, TestRecipes.Read(Registries.BuiltinYaml(name)).Hash.Value);
}
