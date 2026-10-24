using System.Globalization;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// The owner's backstop (2026-10-21, raised from 1M to 5M by q93): a state may use up to 5M tokens per run and nothing parks below that, so no shipped
/// recipe declares a recipe-level token cap and every per-state budget is 5M.
/// </summary>
public sealed class ShippedBudgetsTests
{
    private const int Backstop = 5_000_000;

    private static IEnumerable<(string Path, Recipe Recipe)> Shipped() =>
        ShippedRecipes.Recipes
            .Where(p => p.StartsWith("sindri/", StringComparison.Ordinal))
            .Select(p => (p, RecipeComposition.ReadClean(ShippedRecipes.Read(p), Path.GetFileName(p)).Recipe));

    [Fact, Trait("Lane", "unit")]
    public void No_shipped_recipe_declares_a_recipe_level_token_budget()
    {
        var declaring = Shipped().Where(s => s.Recipe.Budget is not null).Select(s => s.Path).ToList();

        Assert.Empty(declaring);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_shipped_per_state_token_budget_is_the_five_million_backstop()
    {
        var off = Shipped()
            .SelectMany(s => s.Recipe.States.Where(st => st.Value.Budget is not null && st.Value.Budget.Tokens != Backstop).Select(st => string.Create(CultureInfo.InvariantCulture, $"{s.Path}:{st.Key}={st.Value.Budget!.Tokens}")))
            .ToList();

        Assert.Empty(off);
    }
}
