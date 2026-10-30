using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>
/// The pins of one query, resolved once (concept pins spec §3.1): every pin the caller can see, the scope of <c xml:lang="en">concept=</c> when it is set, the pins the
/// anchors sit in, and the note when the pins changed after the map was built. The legs, the graph part and the ranker only read this.
/// </summary>
/// <param name="Pins">The visible pins and what each holds; empty when no repo has a pin.</param>
/// <param name="Scope">The pin <c xml:lang="en">concept=</c> names, or null when it is not set.</param>
/// <param name="AnchorPins">The distinct pins the anchors sit in, in anchor order.</param>
/// <param name="StaleNote">Says pins were edited after the map was built, or null.</param>
internal sealed record PinContext(PinSet Pins, PinScope? Scope, IReadOnlyList<ConceptId> AnchorPins, string? StaleNote)
{
    /// <summary>Where the pin-scoped sources count the results they read and left out; read for the empty-answer note. Unused without <see cref="Scope"/>.</summary>
    internal PinTally Skipped { get; } = new();

    /// <summary>The context of a query with no pins in play.</summary>
    internal static PinContext None { get; } = new(PinSet.Empty, Scope: null, [], StaleNote: null);

    /// <summary>Resolves the pins of a query.</summary>
    /// <param name="mapped">The repos the map is built from.</param>
    /// <param name="visible">The repos the caller may read.</param>
    /// <param name="map">The map the query read (its whole stored graph), or null when there is none.</param>
    /// <param name="concept">The pin <c xml:lang="en">concept=</c> names, or null.</param>
    /// <param name="anchors">The anchors' keys.</param>
    /// <returns>The context; SND-CONTEXT-105 when <paramref name="concept"/> is set and there is no map; 102 to 104 from the pin lookup.</returns>
    internal static Result<PinContext> Of(IReadOnlyList<ContextRepo> mapped, IReadOnlyList<ContextRepo> visible, VisibleMap? map, string? concept, IReadOnlyList<string> anchors)
    {
        if (concept is null && !mapped.Any(r => r.Concepts.Count > 0))
        {
            return None;
        }

        if (map is null)
        {
            return concept is null ? None : ContextErrors.PinNeedsMap.With("concept= needs a built map (fix: sindri context map build)");
        }

        var graph = map.Snapshot.Graph;
        var pins = PinSet.Of(mapped, visible, graph);
        PinScope? named = null;
        if (concept is not null)
        {
            var found = pins.Scope(concept, map.Snapshot.Tag);
            if (found.IsError)
            {
                return found.Error;
            }

            named = found.Value;
        }

        var anchored = anchors.Select(a => NodeKey.TryFrom(a, out var key) ? pins.PinOf(key) : null).OfType<ConceptId>().DistinctBy(p => p.Value, StringComparer.Ordinal).ToList();
        return new PinContext(pins, named, anchored, Stale(map.Snapshot, mapped));
    }

    // Pins are in the rules hash the map was built with, so a different hash means they (or a deny glob) changed since. Computed over the mapped repos, not the caller's narrower view.
    private static string? Stale(MapSnapshot snapshot, IReadOnlyList<ContextRepo> mapped) =>
        snapshot.Version.Ruleset is { } built && MapRuleset.Of(mapped) is var running && !string.Equals(built, running, StringComparison.Ordinal)
            ? $"pins changed after map {snapshot.Tag} was built and the refresh for them didn't finish (fix: Refresh map on the Context page, or sindri context map refresh)"
            : null;
}
