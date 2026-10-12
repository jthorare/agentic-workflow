using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The capability rules (spec §2, §3.2, §3.3, §4.3, W10). A recipe is generated when the caller hands its base in
/// <see cref="ValidationContext.BaseRecipe"/> (the packet freeze, W9); otherwise it is hand-written. On a generated
/// recipe RCP-CAP-001, -002, -005 and -006 keep it at or below its base, comparing uses and guards by path
/// (block plus from -> to, or the state a Step or child is in) and loops by state. On a hand-written recipe
/// RCP-CAP-004 refuses a 'base'. RCP-CAP-007 runs on both. RCP-CAP-003 (the hash is approved and its pins match the
/// build) needs the registry's approvals and runs at every Commit and dispatch; it comes with the registry in P2.
/// </summary>
internal sealed partial class CapabilityRules : IRecipeRules
{
    /// <inheritdoc/>
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        var recipe = document.Recipe;
        var findings = new List<RecipeFinding>();
        if (context.BaseRecipe is { } baseRecipe)
        {
            findings.AddRange(Compare(new Comparison(recipe, baseRecipe, context.Catalog, document.Source)));
        }
        else if (recipe.Base is { } named)
        {
            findings.Add(document.Source.Finding(
                "RCP-CAP-004",
                "base",
                $"hand-written recipe {recipe.Id.Value} names base {named.Id.Value}@{named.Hash.Display}",
                "remove base, which only Sindri's generator writes"));
        }

        findings.AddRange(FactParameters(recipe, context.Catalog, document.Source));
        return findings;
    }

    /// <summary>RCP-CAP-001, -002, -005 and -006, in that order: a generated recipe against its base.</summary>
    private static List<RecipeFinding> Compare(Comparison c)
    {
        var baseUses = UsesOf(c.Base).ToList();
        var bearing = UsesOf(c.Generated).Where(u => IsCapabilityBearing(u, c.Catalog)).ToList();
        var kept = new List<(CapabilityUse Use, IReadOnlyList<CapabilityUse> Candidates)>();
        var findings = new List<RecipeFinding>();
        foreach (var use in bearing)
        {
            var candidates = baseUses.Where(b => b.SameUse(use)).ToList();
            if (candidates.Count == 0)
            {
                findings.Add(c.Source.Finding(
                    "RCP-CAP-001",
                    use.Path,
                    $"capability use {use.Label} {use.Where} isn't in base {c.Base.Id.Value}",
                    "remove it, or generate the recipe from a base that has it"));
            }
            else
            {
                kept.Add((use, candidates));
            }
        }

        findings.AddRange(kept.SelectMany(k => Parameters(c, k.Use, k.Candidates)));
        findings.AddRange(DominatingGuards(c, kept));
        findings.AddRange(Limits(c));
        return findings;
    }

    /// <summary>
    /// RCP-CAP-007: a capability parameter is a literal or '{ profile }', never '{ fact }'. A transition with several
    /// 'from' states names each block once, so each node is checked once.
    /// </summary>
    private static IEnumerable<RecipeFinding> FactParameters(Recipe recipe, BlockCatalog catalog, RecipeSource source)
    {
        foreach (var use in UsesOf(recipe).DistinctBy(u => u.Path, StringComparer.Ordinal))
        {
            if (use.Block is not { } block || catalog.Find(block) is not { } descriptor)
            {
                continue;
            }

            foreach (var (name, value) in use.With)
            {
                if (value.Fact is { } fact && IsCapabilityField(descriptor, name))
                {
                    yield return source.Finding(
                        "RCP-CAP-007",
                        use.ArgumentPath(name),
                        $"capability parameter '{name}' of {use.Label} reads {fact.Value}",
                        "give it a literal or { profile: <key> }, never a fact");
                }
            }
        }
    }

    /// <summary>
    /// Capability-bearing (spec §2): a block whose descriptor declares capabilities or a capability parameter, or (in a
    /// generated recipe, the only place this is asked) a child. A block the catalog doesn't know is RCP-BLOCK-001's.
    /// </summary>
    private static bool IsCapabilityBearing(CapabilityUse use, BlockCatalog catalog) =>
        use.Block is not { } block
        || (catalog.Find(block) is { } d && (d.Capabilities != Capability.None || d.Parameters.Any(p => p.IsCapabilityField)));

    private static bool IsCapabilityField(BlockDescriptor descriptor, string parameter) =>
        descriptor.Parameters.Any(p => p.IsCapabilityField && string.Equals(p.Name, parameter, StringComparison.Ordinal));

    /// <summary>A generated recipe, its base, and where findings are located.</summary>
    /// <param name="Generated">The recipe being validated.</param>
    /// <param name="Base">Its base, from the context.</param>
    /// <param name="Catalog">The build's blocks.</param>
    /// <param name="Source">The generated recipe's file and positions.</param>
    private sealed record Comparison(Recipe Generated, Recipe Base, BlockCatalog Catalog, RecipeSource Source);
}
