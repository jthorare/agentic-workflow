using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>Judges the profile's concept pins against a built map.</summary>
public static class ConceptPins
{
    /// <summary>The pins that hold no feature of the map: no feature key matches their key globs and no feature is implemented in a file under their path globs.</summary>
    /// <param name="graph">The map.</param>
    /// <param name="pins">The pins.</param>
    /// <returns>The pins that match nothing, in the order given.</returns>
    public static IReadOnlyList<ConceptPin> Unmatched(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pins);
        if (pins.Count == 0)
        {
            return [];
        }

        var features = graph.Nodes.Where(n => n.Key.Kind == NodeKind.Feature).Select(n => n.Key.Value).ToList();
        var files = graph.Edges.Where(e => e.Type == EdgeType.Implements && e.From.Kind == NodeKind.Code && e.To.Kind == NodeKind.Feature).Select(e => e.From.Name).Distinct(StringComparer.Ordinal).ToList();
        return [.. pins.Where(p => !features.Exists(f => PathDenylist.MatchesExactCase(f, p.Features)) && !files.Exists(f => PathDenylist.MatchesExactCase(f, p.Paths)))];
    }
}
