using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Features.CheckMap;

/// <summary>What the doctor says about the profile's concept pins against a built map: a pin that matches nothing, and a pin pair that both match one file (the first pin in the list wins it).</summary>
/// <param name="Notes">One line per finding.</param>
/// <param name="Fixes">What to do about each kind of finding.</param>
internal sealed record PinHealth(IReadOnlyList<string> Notes, IReadOnlyList<string> Fixes)
{
    internal static PinHealth Of(GraphSnapshot graph, ContextScope? scope)
    {
        var pins = PinsOf(scope);
        var unmatched = ConceptPins.Unmatched(graph, pins).Select(p => p.Id?.Value ?? p.Name).ToList();
        var overlaps = OverlappingPins(graph, pins);
        var notes = new List<string>();
        var fixes = new List<string>();
        if (unmatched.Count > 0)
        {
            notes.Add($"concept pin {string.Join(", ", unmatched.Select(n => $"'{n}'"))} matches no feature");
            fixes.Add("edit the pin's features or paths under index.concepts in the profile (a pin written for a model cluster key matches nothing now: features come from directories), then approve it");
        }

        if (overlaps.Count > 0)
        {
            notes.AddRange(overlaps);
            fixes.Add("narrow the pins' paths under index.concepts in the profile, or reorder them (the first pin in the list wins a file both match), then approve it");
        }

        return new PinHealth(notes, fixes);
    }

    // The pins of the mapped repos (the profile's first, each once).
    private static List<ConceptPin> PinsOf(ContextScope? scope)
    {
        if (scope is null || SettingsReader.Read(scope.Settings) is not { IsError: false } settings)
        {
            return [];
        }

        return [.. ScopeRules.Mapped(settings.Value, scope.Repos).SelectMany(r => r.Concepts).DistinctBy(p => p.Id?.Value ?? p.Name, StringComparer.Ordinal)];
    }

    // The code files the extractor read; a file node a record made (setup.sh) isn't a file a pin owns.
    private static List<string> ReadFiles(GraphSnapshot graph) =>
        [.. graph.Nodes.Where(n => n.Key.Kind == NodeKind.Code && !UnreadFileNodes.Is(n)).Select(n => n.Key.Name).Order(StringComparer.Ordinal)];

    // A pin pair whose path globs both match a file of the map: the first pin in the list wins that file (spec 2026-10-27 §10.1). One line per pair, with the first file they share.
    private static List<string> OverlappingPins(GraphSnapshot graph, List<ConceptPin> pins)
    {
        var withPaths = pins.Where(p => p.Paths.Count > 0).ToList();
        if (withPaths.Count < 2)
        {
            return [];
        }

        var files = ReadFiles(graph);
        var overlaps = new List<string>();
        for (var i = 0; i < withPaths.Count; i++)
        {
            for (var j = i + 1; j < withPaths.Count; j++)
            {
                var (first, second) = (withPaths[i], withPaths[j]);
                var shared = files.Find(f => PathDenylist.MatchesExactCase(f, first.Paths) && PathDenylist.MatchesExactCase(f, second.Paths));
                if (shared is not null)
                {
                    overlaps.Add($"concept pins '{first.Id?.Value ?? first.Name}' and '{second.Id?.Value ?? second.Name}' both match {shared}");
                }
            }
        }

        return overlaps;
    }
}
