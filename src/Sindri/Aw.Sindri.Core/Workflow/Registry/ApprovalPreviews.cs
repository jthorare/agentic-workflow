using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// What 'recipe approve' and 'recipe show' preview (engine spec §4.2, W8): the capability uses, the effect kinds, the
/// pins, the diff against the last approved version, and for a re-approval of the same hash the blocks that changed.
/// Pure: the slices read the store, the approvals and the wake, then call it.
/// </summary>
internal static class ApprovalPreviews
{
    /// <summary>The preview of approving <paramref name="recipe"/> with <paramref name="pins"/>.</summary>
    /// <param name="recipe">The recipe, read back from the store.</param>
    /// <param name="hash">Its hash.</param>
    /// <param name="pins">What the approval will pin.</param>
    /// <param name="catalog">This build's blocks.</param>
    /// <param name="latest">The latest approval of the same hash, for a re-approval; else null.</param>
    /// <returns>The preview, without a diff or resumes (the planner adds those from the store and the wake).</returns>
    public static ApprovalPreview Approve(Recipe recipe, RecipeHash hash, RecipePins pins, BlockCatalog catalog, RecipeApproval? latest)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(pins);
        ArgumentNullException.ThrowIfNull(catalog);
        var changed = latest is null ? [] : Changed(latest.Pins, pins);
        return new ApprovalPreview(recipe.Id, recipe.Version, hash, recipe.Base, Diff: null, CapabilityUses(recipe, catalog), Effects(recipe, allowlist: null), pins,
            changed, Resumes: [], Evals(recipe));
    }

    /// <summary>The eval line of the preview: the files the recipe declares (P4 runs them at approve).</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>The line, or null when it declares none (approving it is refused with SND-RECIPE-008).</returns>
    public static string? Evals(Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return recipe.Evals.Count == 0
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{recipe.Evals.Count} declared: {string.Join(", ", recipe.Evals)} (run at approve from P4)");
    }

    /// <summary>The normalized text written one member per line, so a diff reads line by line.</summary>
    /// <param name="normalized">The stored normalized text (compact JSON).</param>
    /// <returns>The indented JSON.</returns>
    public static string Pretty(string normalized)
    {
        using var document = JsonDocument.Parse(normalized);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            document.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Each capability-bearing block use (spec §2), where it sits: a Step or Step input by its state, a gate or effect by its transition.</summary>
    private static List<PreviewCapabilityUse> CapabilityUses(Recipe recipe, BlockCatalog catalog)
    {
        var uses = recipe.States
            .SelectMany(static s => s.Value.Step is { } step
                ? step.Inputs.Select(static i => (i.Use, i.With)).Prepend((step.Use, step.With)).Select(u => (u.Use, u.With, From: s.Key, To: s.Key))
                : [])
            .Concat(recipe.Transitions.SelectMany(static t => t.From.SelectMany(from =>
                t.Gates.Select(g => (g.Use, g.With)).Concat(t.Effects.Select(e => (e.Use, e.With)))
                    .Select(u => (u.Use, u.With, From: from, t.To)))));
        var found = new List<PreviewCapabilityUse>();
        foreach (var (use, with, from, to) in uses)
        {
            if (catalog.Find(use) is not { } d)
            {
                continue;
            }

            var fields = d.Parameters.Where(static p => p.IsCapabilityField).Select(static p => p.Name).ToList();
            if (d.Capabilities == Capability.None && fields.Count == 0)
            {
                continue;
            }

            found.Add(new PreviewCapabilityUse(use, from, to, d.Capabilities,
                [.. fields.Where(with.ContainsKey).Select(n => $"{n}={Show(with[n])}")]));
        }

        return found;
    }

    /// <summary>The effect kinds the recipe emits, each marked against the approved profile's effect allowlist.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="allowlist">The approved profile's 'workflow.effects'; null when it sets none (then none is on it).</param>
    /// <returns>The kinds, ordered by id.</returns>
    public static IReadOnlyList<PreviewEffect> Effects(Recipe recipe, IReadOnlySet<BlockId>? allowlist) =>
        [.. recipe.Transitions.SelectMany(static t => t.Effects).Select(static e => e.Use).Distinct()
            .OrderBy(static id => id.Value, StringComparer.Ordinal)
            .Select(id => new PreviewEffect(id, Allowlisted: allowlist?.Contains(id) ?? false))];   // why: no allowlist set means none is on it (RCP-EFFECT-001 only warns)

    private static IReadOnlyList<PreviewBlockChange> Changed(RecipePins approved, RecipePins build) =>
        [.. build.Blocks.SelectMany(b => approved.Blocks.Where(a => a.Block == b.Block && a.Hash != b.Hash)
            .Select(a => new PreviewBlockChange(b.Block, a.Hash, b.Hash, Capability.None, Capability.None)))];

    private static string Show(WithValue value) => value switch
    {
        { Literal: { } literal } => literal.GetRawText(),
        { Fact: { } fact } => $"{{ fact: {fact.Value} }}",
        _ => $"{{ profile: {value.Profile} }}",
    };
}
