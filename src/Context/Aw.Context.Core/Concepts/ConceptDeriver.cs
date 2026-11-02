using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>Derives the concepts of a map (concepts note §2): groups of features from the part-of roots, small or strongly linked groups merged (no fixed count; ties go to the smaller group and none passes 3x the average area), pins first, names from a model or a deterministic fallback.</summary>
public static class ConceptDeriver
{
    /// <summary>The derived groups, for the namer: pinned concepts are not among them.</summary>
    /// <param name="graph">The map's graph, without concepts.</param>
    /// <param name="pins">The owner's pinned concepts.</param>
    /// <returns>The groups in seed order.</returns>
    public static IReadOnlyList<ConceptGroup> Groups(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins) => Groups(graph, pins, glossary: null);

    /// <summary>The concept nodes and the belongs-to edges of a map.</summary>
    /// <param name="graph">The map's graph, without concepts.</param>
    /// <param name="pins">The owner's pinned concepts.</param>
    /// <param name="names">What a model named the groups, or null for the fallback.</param>
    /// <returns>The concepts, 'concept:other' among them when the map has a feature, and the membership edges.</returns>
    public static ExtractedMap Derive(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins, ConceptNames? names) => Derive(graph, pins, names, glossary: null);

    /// <summary>The derived groups, for the namer, with the glossary's terms on each group.</summary>
    /// <param name="graph">The map's graph, without concepts.</param>
    /// <param name="pins">The owner's pinned concepts.</param>
    /// <param name="glossary">The map's glossary, or null when it has none.</param>
    /// <returns>The groups in seed order.</returns>
    public static IReadOnlyList<ConceptGroup> Groups(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins, MapGlossary? glossary) => Groups(graph, pins, glossary, []);

    /// <summary>The concept nodes and the belongs-to edges of a map, grouped with the glossary as one more signal and carrying their terms.</summary>
    /// <param name="graph">The map's graph, without concepts.</param>
    /// <param name="pins">The owner's pinned concepts.</param>
    /// <param name="names">What a model named the groups, or null for the fallback.</param>
    /// <param name="glossary">The map's glossary, or null when it has none.</param>
    /// <returns>The concepts, 'concept:other' among them when the map has a feature, and the membership edges.</returns>
    public static ExtractedMap Derive(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins, ConceptNames? names, MapGlossary? glossary) => Derive(graph, pins, names, glossary, []);

    /// <summary>The derived groups, for the namer, shaped by the scan's concept hints.</summary>
    /// <param name="graph">The map's graph, without concepts.</param>
    /// <param name="pins">The owner's pinned concepts.</param>
    /// <param name="glossary">The map's glossary, or null when it has none.</param>
    /// <param name="hints">The concept hints of the last scan, or none.</param>
    /// <returns>The groups in seed order.</returns>
    public static IReadOnlyList<ConceptGroup> Groups(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins, MapGlossary? glossary, IReadOnlyList<ConceptHint> hints)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pins);
        var view = new ConceptView(graph);
        var (_, derived) = ConceptPlan.Build(graph, pins, glossary, hints);
        return [.. derived.Select(c => new ConceptGroup(c.Seed, view.Label(c.Seed), [.. c.Features.Select(view.Label)], view.RecordTitles(c.Features)) { Terms = c.Terms })];
    }

    /// <summary>The concept nodes and the belongs-to edges of a map, shaped by the scan's concept hints.</summary>
    /// <param name="graph">The map's graph, without concepts.</param>
    /// <param name="pins">The owner's pinned concepts.</param>
    /// <param name="names">What a model named the groups, or null for the fallback.</param>
    /// <param name="glossary">The map's glossary, or null when it has none.</param>
    /// <param name="hints">The concept hints of the last scan, or none.</param>
    /// <returns>The concepts, 'concept:other' among them when the map has a feature, and the membership edges.</returns>
    public static ExtractedMap Derive(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins, ConceptNames? names, MapGlossary? glossary, IReadOnlyList<ConceptHint> hints)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pins);
        var view = new ConceptView(graph);
        var (pinned, derived) = ConceptPlan.Build(graph, pins, glossary, hints);
        if (view.FeatureKeys.Count == 0 && pinned.Count == 0)
        {
            return ExtractedMap.Empty;
        }

        var concepts = pinned.Concat(derived).Select(c => ConceptNodes.Concept(c, view, names)).ToList();
        return ConceptNodes.Join(view, concepts);
    }
}
