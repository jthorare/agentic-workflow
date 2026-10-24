using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Setup;

/// <summary>Counts the recipes for the setup list.</summary>
public static class SetupRecipes
{
    /// <summary>
    /// Each recipe id once, at its highest registered version (the one that would run); a recipe whose highest version is revoked can't be approved and isn't counted.
    /// Approved means that version is approved at its current hash and the approval isn't stale. A recipe the shipped plan still names (never registered, approved at an older hash, stale, or waiting for
    /// its own approval) is not approved whatever the registry says, and it counts even when nothing is registered for it yet: the plan is the one reading of what waits, so the
    /// setup step and approve-all cannot disagree.
    /// </summary>
    /// <param name="lines">Every registered recipe line, by id and version.</param>
    /// <param name="waiting">The shipped plan now: what approve-all would approve and what needs an approval of its own.</param>
    /// <returns>How many can be approved and how many of those are.</returns>
    public static RecipesFact Count(IReadOnlyList<RecipeSummary> lines, ShippedRecipePlan waiting)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(waiting);
        var pending = waiting.Recipes.Select(static r => r.Id).Concat(waiting.OwnApproval.Select(static o => o.Id)).ToHashSet();
        var current = lines.GroupBy(static l => l.Id).Select(static g => g.MaxBy(static l => l.Version)).OfType<RecipeSummary>()
            .Where(l => l.Standing != RecipeStanding.Revoked && !pending.Contains(l.Id))
            .ToList();
        return new RecipesFact(current.Count + pending.Count, current.Count(static l => l is { Standing: RecipeStanding.Approved, Stale.Count: 0 }));
    }
}
