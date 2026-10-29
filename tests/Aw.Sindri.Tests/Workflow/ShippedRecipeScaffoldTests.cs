using System.Collections.Concurrent;
using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>'recipe eval --scaffold' over the shipped block recipes (DT-R2 plan U7): every generated case passes the runner.</summary>
public sealed class ShippedRecipeScaffoldTests
{
    public static TheoryData<string> Recipes => [.. ShippedRecipes.Recipes.Where(r => r.StartsWith("sindri/", StringComparison.Ordinal))];

    private static readonly ConcurrentDictionary<string, Core.Workflow.Recipes.Recipe> _recipes = new(StringComparer.Ordinal);   // each shipped recipe parsed once per run; Recipe is an immutable record

    private static readonly Dictionary<string, string[]> _routes = new(StringComparer.Ordinal)
    {
        ["sindri/workflow.evidence.yaml"] = ["scaffold-ui", "scaffold-api", "scaffold-db", "scaffold-desktop", "scaffold-all", "scaffold-none", "scaffold-other", "scaffold-uncertain", "scaffold-step_failed"],
        ["sindri/workflow.reproduce.yaml"] = ["scaffold-uiCheck", "scaffold-testCheck", "scaffold-uncertain", "scaffold-step_failed"],
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Recipes))]
    public void Every_scaffolded_case_of_a_shipped_recipe_passes(string path)
    {
        var recipe = _recipes.GetOrAdd(path, static p => EvalComposition.Recipe(p));
        var children = InMemoryChildRecipes.None;
        foreach (var sibling in ShippedRecipes.Recipes.Where(r => r.StartsWith("sindri/", StringComparison.Ordinal)))
        {
            var child = _recipes.GetOrAdd(sibling, static p => EvalComposition.Recipe(p));
            _ = children.With(new ChildRecipe(child, child.Hash!.Value));
        }

        var cases = EvalScaffold.GenerateAll(recipe, EvalComposition.Catalog, children);

        Assert.NotEmpty(cases);
        if (_routes.TryGetValue(path, out var expected))
        {
            Assert.Equal(expected, cases.Select(c => c.Name), StringComparer.Ordinal);
            Assert.All(cases, c => Assert.NotNull(c.Steps[^1].Expect.Outcome));
        }

        foreach (var evalCase in cases)
        {
            var verdict = EvalRunner.Run(evalCase, recipe, EvalComposition.Catalog, children);
            Assert.True(verdict.Passed, $"{path} {evalCase.Name}: line {verdict.Line?.ToString(CultureInfo.InvariantCulture)}: {verdict.Detail}");
        }
    }
}
