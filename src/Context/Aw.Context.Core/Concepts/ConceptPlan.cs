using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;
using Aw.Context.Core.WholeMap;

namespace Aw.Context.Core.Concepts;

/// <summary>The grouping half of <see cref="ConceptDeriver"/>: pins first, then the area groups of what is left, merged down to at most 15.</summary>
internal static class ConceptPlan
{
    internal const int MaxConcepts = 15;
    internal const int MinFeatures = 3;
    internal const int WholeBelow = 5;
    internal const int TermsPerConcept = 6;

    internal static (IReadOnlyList<PlannedConcept> Pinned, IReadOnlyList<PlannedConcept> Derived) Build(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins, MapGlossary? glossary = null, IReadOnlyList<ConceptHint>? hints = null)
    {
        var features = graph.Nodes.Where(n => n.Key.Kind == NodeKind.Feature).Select(n => n.Key.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var taken = new HashSet<string>(StringComparer.Ordinal) { ConceptKeys.Other["concept:".Length..] };
        var pinned = pins.Select(p => new PlannedConcept(ConceptSlug.Unique(p.Id?.Value ?? p.Name, taken), "", p, Pinned(graph, features, p))).Select(WithSeed).Select(c => WithTerms(c, glossary)).ToList();
        var left = features.Except(pinned.SelectMany(p => p.Features), StringComparer.Ordinal).ToList();
        var areas = Groups(graph, left);
        var (shaped, hinted) = hints is { Count: > 0 } ? ConceptHintGroups.Apply(areas, hints) : (areas, new HashSet<string>(StringComparer.Ordinal));
        var groups = ConceptGrouping.Merge(graph, shaped, glossary is null ? null : new GlossaryTermIndex(glossary), hinted);
        var derived = groups.OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new PlannedConcept(ConceptSlug.Unique(g.Key["feature:".Length..], taken), g.Key, Pin: null, g.Value)).Select(c => WithTerms(c, glossary)).ToList();
        return (pinned, derived);
    }

    private static PlannedConcept WithTerms(PlannedConcept concept, MapGlossary? glossary) => concept with { Terms = GlossaryTermPicker.Top(glossary, concept.Features, TermsPerConcept) };

    private static PlannedConcept WithSeed(PlannedConcept pin) => pin with { Seed = pin.Features.Count > 0 ? pin.Features[0] : "" };

    // The features a pin names by key or key glob, and those implemented by a code file under one of its path globs.
    private static List<string> Pinned(GraphSnapshot graph, List<string> features, ConceptPin pin)
    {
        var byKey = features.Where(f => PathDenylist.MatchesExactCase(f, pin.Features));
        var byPath = pin.Paths.Count == 0
            ? []
            : graph.Edges.Where(e => e.Type == EdgeType.Implements && e.From.Kind == NodeKind.Code && e.To.Kind == NodeKind.Feature && PathDenylist.MatchesExactCase(e.From.Name, pin.Paths)).Select(e => e.To.Value);
        return [.. byKey.Concat(byPath).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    // The area rules group a map of five or more features; a smaller map has a concept per feature.
    private static Dictionary<string, List<string>> Groups(GraphSnapshot graph, List<string> left)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var areas = left.Count < WholeBelow ? null : MapAreas.Assign(graph);
        foreach (var feature in left)
        {
            var seed = areas is not null && areas.ByNode.TryGetValue(feature, out var area) && area.Area != AreaIndex.UnassignedKey ? area.Area : feature;
            if (!groups.TryGetValue(seed, out var members))
            {
                groups[seed] = members = [];
            }

            members.Add(feature);
        }

        return groups;
    }
}
