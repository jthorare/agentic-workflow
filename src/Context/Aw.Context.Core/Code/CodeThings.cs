using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.Code;

/// <summary>The data things the own scope reads or writes (own scope only: the cascade's state hop is the lenses' business).</summary>
internal static class CodeThings
{
    internal static List<CodeThing> Of(CodeReach reach)
    {
        // A code file that implements something in the own scope counts as in it: its reads and writes (the map build's data-store edges) are the scope's.
        var implemented = reach.Into(EdgeType.Implements).Where(e => e.From.Kind == NodeKind.Code)
            .GroupBy(e => e.From.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(e => e.To.Value).ToList(), StringComparer.Ordinal);
        return [.. reach.Graph.Edges
            .Where(e => e.Type is EdgeType.Reads or EdgeType.Writes && (reach.Union.Contains(e.From.Value) || implemented.ContainsKey(e.From.Value)) && reach.Nodes.TryGetValue(e.To.Value, out var to) && to.Key.Kind == NodeKind.Thing)
            .GroupBy(e => e.To.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => Thing(reach, [.. g], implemented))];
    }

    private static CodeThing Thing(CodeReach reach, List<GraphEdge> edges, Dictionary<string, List<string>> implemented)
    {
        var node = reach.Nodes[edges[0].To.Value];
        var reads = edges.Exists(e => e.Type == EdgeType.Reads);
        var writes = edges.Exists(e => e.Type == EdgeType.Writes);
        var via = edges.Select(e => e.From).DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal).ToList();
        var confirmed = LensScope.Confirmed(node.Provenance);
        return new CodeThing(
            node.Key,
            node.Label,
            NodeKind.Thing,
            reads && writes ? DataAccess.Both : writes ? DataAccess.Writes : DataAccess.Reads,
            via,
            CodeLensBuilder.ThingTerms(reach.Graph, node.Key).Count > 0,
            node.Provenance,
            node.Confidence,
            confirmed,
            TrustPolicy.ForNode(node, confirmed),
            reach.AnchorsReaching(via.SelectMany(k => implemented.TryGetValue(k.Value, out var targets) ? targets.Prepend(k.Value) : [k.Value])));
    }
}
