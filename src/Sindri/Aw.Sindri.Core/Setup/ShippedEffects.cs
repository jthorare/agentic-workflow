using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Setup;

/// <summary>The effect kinds a set of recipes declares (setup-once spec §5.1): what the default profile's 'workflow.effects' must allow.</summary>
public static class ShippedEffects
{
    /// <summary>The union of the effects every transition of <paramref name="recipes"/> emits.</summary>
    /// <param name="recipes">The recipes.</param>
    /// <returns>The effect ids, sorted, each once.</returns>
    public static IReadOnlyList<string> Union(IEnumerable<Recipe> recipes)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        return [.. recipes.SelectMany(static r => r.Transitions).SelectMany(static t => t.Effects).Select(static e => e.Use.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }
}
