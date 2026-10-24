using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// What a recipe can do (setup-once spec §5.3): the sorted set of the block ids it names, steps, inputs, gates, evidence providers and effects (an effect kind is the id of its block).
/// The plan, its digest and the gain rule all use this one function, and the old set of a recipe comes from its last approved hash's stored text, so a later version that counts more can
/// only make more recipes look like gains, the safe direction. Pure.
/// </summary>
public static class RecipeCapabilitySet
{
    /// <summary>The capabilities of <paramref name="recipe"/>, each with its plain sentence (the block's id when this build has none).</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">This build's blocks, whose sentences name the capabilities.</param>
    /// <returns>The lines, sorted by id.</returns>
    public static IReadOnlyList<CapabilityLine> Of(Recipe recipe, BlockCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(catalog);
        return
        [
            .. PinRules.NamedBlocks(recipe)
                .Select(id => new CapabilityLine(CapabilityId.From(id.Value), catalog.Find(id)?.Sentence ?? id.Value))
                .OrderBy(static c => c.Id.Value, StringComparer.Ordinal),
        ];
    }

    /// <summary>What <paramref name="now"/> has that <paramref name="before"/> lacked, and what it lost.</summary>
    /// <param name="before">The capabilities of the last approved hash (empty for a recipe never approved).</param>
    /// <param name="now">The capabilities of the shipped hash.</param>
    /// <returns>The delta.</returns>
    public static CapabilityDelta Delta(IReadOnlyList<CapabilityLine> before, IReadOnlyList<CapabilityLine> now)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(now);
        return new CapabilityDelta(
            [.. now.Where(n => !before.Any(b => b.Id == n.Id))],
            [.. before.Where(b => !now.Any(n => n.Id == b.Id))]);
    }
}
