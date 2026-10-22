using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// The recipes an instance of a recipe can start besides itself: the ones its child waits name (spec §4.1) and, for the line (LN-WF1),
/// the ones its 'effect.line.start-phase' effects name in their literal 'recipe' argument. Both are pinned and approved with the parent,
/// so a linked instance is created from the hash its parent pinned, as a child is.
/// </summary>
public static class RecipeChildren
{
    /// <summary>The effect whose literal 'recipe' argument names a linked recipe.</summary>
    public const string StartPhase = "effect.line.start-phase";

    /// <summary>The recipes <paramref name="recipe"/> starts: its child waits' and its start-phase effects', in file order, each once.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>The recipe ids.</returns>
    public static IEnumerable<RecipeId> Of(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return recipe.States.Values.Select(static s => s.WaitFor?.Child?.Recipe).OfType<RecipeId>().Concat(Linked(recipe)).Distinct();
    }

    /// <summary>The recipes <paramref name="recipe"/>'s start-phase effects name.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>The recipe ids; an effect with no literal 'recipe' (or one that isn't a recipe id) names none.</returns>
    public static IEnumerable<RecipeId> Linked(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return Named(recipe);
    }

    private static IEnumerable<RecipeId> Named(Recipe recipe)
    {
        foreach (var effect in recipe.Transitions.SelectMany(static t => t.Effects))
        {
            if (string.Equals(effect.Use.Value, StartPhase, StringComparison.Ordinal)
                && effect.With.GetValueOrDefault("recipe")?.Literal is { ValueKind: JsonValueKind.String } literal
                && literal.GetString() is { } text && RecipeId.TryFrom(text, out var id))
            {
                yield return id;
            }
        }
    }
}
