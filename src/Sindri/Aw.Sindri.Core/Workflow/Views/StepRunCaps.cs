using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>
/// The loop cap of each looped state of a recipe, for the Step runs' max attempts. A state is in a loop by the recipe's
/// own field ('loop'), so a state at neither end of the back edge counts too. The cap is the allowance the instance holds
/// for the loop (a granted round raises it) when the active state is in the loop and its cell shows it, else the
/// recipe's cap; every run of a state reads the same value.
/// </summary>
public static class StepRunCaps
{
    /// <summary>The cap of each state of <paramref name="recipe"/> that is in a loop; a state in no loop has none.</summary>
    /// <param name="recipe">The instance's recipe.</param>
    /// <param name="states">The instance's state cells, whose active round gives the allowance of its loop.</param>
    /// <returns>State id to cap.</returns>
    public static IReadOnlyDictionary<string, int> Of(Recipe recipe, IReadOnlyList<StateCell> states)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(states);
        var caps = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, state) in recipe.States)
        {
            if (state.Loop is { } name && recipe.Loops.TryGetValue(name, out var spec))
            {
                caps[id] = Allowance(name, recipe, states) ?? spec.Cap;
            }
        }

        return caps;
    }

    // The active cell of a state in the loop shows the allowance the instance holds for it (a grant raises it).
    private static int? Allowance(string loop, Recipe recipe, IReadOnlyList<StateCell> states) =>
        states.Where(c => c.Round is not null && recipe.States.TryGetValue(c.State, out var s) && string.Equals(s.Loop, loop, StringComparison.Ordinal)).Select(static c => c.Round?.Max).FirstOrDefault();
}
