using System.Text.RegularExpressions;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The recipes under recipes/ (spec §12): the real ones, the dev ones, and their evals (§9.2).</summary>
public sealed partial class ShippedRecipesTests
{
    public static TheoryData<string> Recipes => [.. ShippedRecipes.Recipes];

    public static TheoryData<string> Evals => [.. ShippedRecipes.Evals];

    [Fact, Trait("Lane", "unit")]
    public void The_real_and_dev_recipes_are_shipped() =>
        Assert.Equal(
            ["dev/workflow.dev-notify.yaml", "dev/workflow.dev-timeout.yaml", "sindri/workflow.item.yaml", "sindri/workflow.step.yaml"],
            ShippedRecipes.Recipes,
            StringComparer.Ordinal);

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Shipped_recipe_reads_without_findings(string path) =>
        _ = RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path));

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Shipped_recipe_validates_against_the_shipped_catalog(string path)
    {
        var document = RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path));

        Assert.Empty(RecipeComposition.Errors(document, FixtureCatalog.Shipped(), Siblings(path)));
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Shipped_recipe_lists_every_eval_of_its_own_and_each_one_is_shipped(string path)
    {
        var document = RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path));
        var directory = Path.GetDirectoryName(path);
        var own = ShippedRecipes.Evals.Where(e => e.StartsWith($"{directory}/evals/{document.Recipe.Id.Value}/", StringComparison.Ordinal));

        Assert.Equal(own, document.Recipe.Evals.Select(e => $"{directory}/{e}").Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Shipped_recipe_holds_no_approval_data(string path) =>
        Assert.False(ApprovalData.In(ShippedRecipes.Read(path)), path);

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Shipped_recipe_has_a_happy_eval(string path)
    {
        var id = RecipeIdLine.Match(ShippedRecipes.Read(path)).Groups["id"].Value;

        Assert.Contains($"{Path.GetDirectoryName(path)}/evals/{id}/happy.jsonl", ShippedRecipes.Evals, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Evals))]
    public void Eval_file_follows_the_eval_format(string path)
    {
        var lines = ShippedRecipes.Read(path).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.True(lines.Length >= 2, $"{path}: a start line and at least one trigger line");
        EvalFormat.AssertStart(lines[0]);
        Assert.All(lines.Skip(1), EvalFormat.AssertStep);
    }

    private static InMemoryChildRecipes Siblings(string path)
    {
        var children = InMemoryChildRecipes.None;
        foreach (var sibling in ShippedRecipes.Recipes.Where(r => Path.GetDirectoryName(r) == Path.GetDirectoryName(path)))
        {
            var reading = RecipeComposition.Read(ShippedRecipes.Read(sibling), Path.GetFileName(sibling));
            if (reading.Document is { } document)
            {
                _ = children.With(new ChildRecipe(document.Recipe, document.Hash));
            }
        }

        return children;
    }

    [GeneratedRegex(@"^id:\s*(?<id>\S+)\s*$", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RecipeIdLine { get; }
}
