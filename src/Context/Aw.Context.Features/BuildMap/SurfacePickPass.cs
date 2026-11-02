using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Models;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Surfaces;

namespace Aw.Context.Features.BuildMap;

/// <summary>The product-surface step of a map build (model routing spec §6.9): groups the build's action lookalikes, reuses the decisions already stored, asks Jev about the rest and turns the verdicts into override rows.</summary>
/// <param name="picker">Jev's decision on groups.</param>
/// <param name="store">The decisions already made, by group key.</param>
/// <param name="port">The choice port, for the identity stored with a decision; null when the host has none.</param>
/// <param name="clock">The time of the stored decisions and the rows.</param>
internal sealed class SurfacePickPass(ISurfacePicker picker, ISurfacePickStore store, IChoicePort? port, TimeProvider clock)
{
    /// <summary>Runs the step over what this build found.</summary>
    /// <param name="settings">The approved settings.</param>
    /// <param name="found">The map this build found.</param>
    /// <param name="overrides">The current map's overrides.</param>
    /// <param name="spentTokens">What labeling spent of 'context.labeling.maxTokens' this build.</param>
    /// <param name="cancellationToken">Cancels the step.</param>
    /// <returns>The rows and the note; never an error.</returns>
    public async ValueTask<SurfacePickOutcome> Run(ContextSettings settings, ExtractedMap found, IReadOnlyList<MapOverride> overrides, int spentTokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var groups = SurfaceCandidates.Groups(found, overrides);
        if (groups.Count == 0)
        {
            return SurfacePickOutcome.None;
        }

        var stored = await store.Get([.. groups.Select(static g => g.Key)], cancellationToken).ConfigureAwait(false);   // a store that can't be read is all misses, never a failed build
        var hits = stored.IsError ? new Dictionary<string, SurfaceDecision>(StringComparer.Ordinal) : stored.Value;
        var verdicts = groups.Where(g => hits.ContainsKey(g.Key)).Select(g => hits[g.Key].ToVerdict(g)).ToList();
        var misses = groups.Where(g => !hits.ContainsKey(g.Key)).ToList();
        string? note = null;
        if (misses.Count > 0)
        {
            var asked = await picker.Decide(misses, new SurfacePickOptions(settings.Models.SurfacePick == LabelPick.Jev, settings.ExtraPatterns, spentTokens, settings.Labeling.MaxTokens), cancellationToken).ConfigureAwait(false);
            verdicts.AddRange(asked.Verdicts);
            note = asked.StatusLine;
            await Remember(misses, asked.Verdicts, cancellationToken).ConfigureAwait(false);
        }

        return new SurfacePickOutcome(SurfaceApply.Rows(groups, verdicts, overrides, clock.GetUtcNow()), note);
    }

    // Only a verdict Jev decided is kept (q149); a store that can't be written costs the next build a call, never this one its rows.
    private async ValueTask Remember(List<SurfaceGroup> asked, IReadOnlyList<SurfaceVerdict> verdicts, CancellationToken cancellationToken)
    {
        var byKey = asked.ToDictionary(static g => g.Key, StringComparer.Ordinal);
        var at = clock.GetUtcNow();
        var model = port?.Identity.Value;
        var decided = verdicts.Where(v => v.Decided && byKey.ContainsKey(v.GroupKey)).Select(v => SurfaceDecision.From(v, byKey[v.GroupKey], model, at)).ToList();
        if (decided.Count > 0)
        {
            _ = await store.Save(decided, cancellationToken).ConfigureAwait(false);
        }
    }
}
