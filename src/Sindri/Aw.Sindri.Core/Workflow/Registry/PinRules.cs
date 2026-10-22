using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// The pins (spec §2 descriptor hash, §4.1, §4.3 RCP-CAP-003, W11): what an approval pins, the digest a drift
/// challenge binds, and whether an approval still holds against this build. Pure.
/// </summary>
public static class PinRules
{
    private const string Scheme = "sha256:";

    /// <summary>The descriptor hash of <paramref name="descriptor"/>: SHA-256 over its RFC 8785 canonical JSON.</summary>
    /// <param name="descriptor">The block's descriptor.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The hash.</returns>
    public static DescriptorHash Hash(BlockDescriptor descriptor, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(options);
        return DescriptorHash.From(Sha256(descriptor, options));
    }

    /// <summary>
    /// The block pins of <paramref name="recipe"/>: each block it names (its Steps and their inputs, gates, effects, and
    /// the evidence providers of its evidence kinds) with its descriptor hash in <paramref name="catalog"/>, once each,
    /// sorted by id. A block the catalog doesn't have gets no pin (the validator reports it), so a block an approval
    /// pinned that this build lost reads as drift.
    /// </summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">This build's block catalog.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The pins.</returns>
    public static IReadOnlyList<BlockPin> Blocks(Recipe recipe, BlockCatalog catalog, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);
        return
        [
            .. Named(recipe)
                .Distinct()
                .Select(catalog.Find)
                .OfType<BlockDescriptor>()
                .OrderBy(d => d.Id.Value, StringComparer.Ordinal)
                .Select(d => new BlockPin(d.Id, Hash(d, options))),
        ];
    }

    /// <summary>
    /// The child pins of <paramref name="recipe"/> (spec §4.1): each child recipe it waits on, with the hash of that
    /// child's latest approved version in <paramref name="current"/>, sorted by id. A child with no current approval has
    /// no pin.
    /// </summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="current">The latest approval of every key that isn't revoked (IRecipeApprovals.Current).</param>
    /// <returns>The pins.</returns>
    public static IReadOnlyList<ChildPin> Children(Recipe recipe, IEnumerable<RecipeApproval> current)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(current);
        var approvals = current.ToList();
        return
        [
            .. RecipeChildren.Of(recipe)
                .Select(child => approvals.Where(a => a.Id == child).MaxBy(a => a.Version))
                .OfType<RecipeApproval>()
                .OrderBy(a => a.Id.Value, StringComparer.Ordinal)
                .Select(a => new ChildPin(a.Id, a.Hash)),
        ];
    }

    /// <summary>What an approval of <paramref name="recipe"/> pins against this build: its block pins and its child pins.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">This build's block catalog.</param>
    /// <param name="current">The latest approval of every key that isn't revoked.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The pins.</returns>
    public static RecipePins Pins(Recipe recipe, BlockCatalog catalog, IEnumerable<RecipeApproval> current, JsonSerializerOptions options) =>
        new(Blocks(recipe, catalog, options), Children(recipe, current));

    /// <summary>The digest every approval's challenge binds (spec §4.2, bound on first approvals too since review run 1): SHA-256 over the canonical JSON of <paramref name="pins"/>.</summary>
    /// <param name="pins">The pins the approval will record.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>'sha256:' and 64 hex digits.</returns>
    public static string Digest(RecipePins pins, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(options);
        return Sha256(pins, options);
    }

    /// <summary>
    /// RCP-CAP-003: the approval holds when it exists, isn't revoked, and its block pins equal <paramref name="build"/>.
    /// Only blocks count: a changed child is never drift, it only marks the parent child-stale (spec §4.1).
    /// </summary>
    /// <param name="approval">The latest approval of the key, or null.</param>
    /// <param name="revocation">The hash's revocation, or null.</param>
    /// <param name="build">This build's block pins for the recipe.</param>
    /// <returns>Holds, Revoked or Drifted; null when it was never approved (SND-RECIPE-003).</returns>
    public static ApprovalCheck? Check(RecipeApproval? approval, RecipeRevocation? revocation, IReadOnlyList<BlockPin> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return approval switch
        {
            null => null,
            _ when revocation is not null => ApprovalCheck.Revoked,
            _ when approval.Pins.Blocks.SequenceEqual(build) => ApprovalCheck.Holds,
            _ => ApprovalCheck.Drifted,
        };
    }

    /// <summary>Every block id the recipe names, in file order, repeats included.</summary>
    private static IEnumerable<BlockId> Named(Recipe recipe)
    {
        foreach (var step in recipe.States.Values.Select(s => s.Step).OfType<StepUse>())
        {
            yield return step.Use;
            foreach (var input in step.Inputs)
            {
                yield return input.Use;
            }
        }

        foreach (var transition in recipe.Transitions)
        {
            foreach (var id in transition.Gates.Select(g => g.Use).Concat(transition.Effects.Select(e => e.Use)))
            {
                yield return id;
            }

            foreach (var check in transition.Evidence)
            {
                if (BlockId.TryFrom("evidence." + check.Kind, out var provider))
                {
                    yield return provider;
                }
            }
        }
    }

    /// <summary>'sha256:' and the hex SHA-256 of <paramref name="value"/>'s RFC 8785 canonical JSON, as the AwJson options write it.</summary>
    private static string Sha256<T>(T value, JsonSerializerOptions options) =>
        Scheme + Convert.ToHexStringLower(SHA256.HashData(CanonicalJson.Of(JsonNode.Parse(AwJson.Serialize(value, options)))));
}
