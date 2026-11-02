using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Packing;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.QueryContext;

/// <summary>The graph part under <c xml:lang="en">concept=</c> (concept pins spec §3.2): only the pin's nodes, the anchors, other pins and the data stores the pin's nodes use.</summary>
internal static class PinCut
{
    /// <summary>Cuts a graph part to one pin.</summary>
    /// <param name="part">The graph part as the anchors built it.</param>
    /// <param name="scope">The pin.</param>
    /// <param name="graph">The graph the part was read from.</param>
    /// <returns>The part with only the pin's candidates, the anchors, other pins and the data stores the pin's nodes use.</returns>
    internal static GraphPart Apply(GraphPart part, PinScope scope, GraphSnapshot graph)
    {
        var inside = Inside(part, scope, graph);
        var kept = part.Candidates.Where(c => Keeps(c, inside, graph)).ToList();
        var decided = kept.Select(k => k.Item.Node).ToHashSet(StringComparer.Ordinal);
        return part with
        {
            Candidates = kept,
            Cites = kept.Count == 0 ? new Dictionary<string, Citation>(StringComparer.Ordinal) : part.Cites,
            Conflicts = [.. part.Conflicts.Where(c => inside.Contains(c.Node))],
            DecisionRecords = part.DecisionRecords.Where(d => decided.Contains(d.Key)).ToDictionary(d => d.Key, d => d.Value, StringComparer.Ordinal),
            LinkedRecords = [.. part.LinkedRecords.Where(r => NodeKey.TryFrom("record:" + r, out var key) && scope.HoldsNode(key))],
        };
    }

    // The pin's nodes plus the anchors, plus the surfaces and actions that hang off them (the pin map groups features, files and records, and a feature owns its surfaces and their actions).
    private static HashSet<string> Inside(GraphPart part, PinScope scope, GraphSnapshot graph)
    {
        var inside = graph.Nodes.Select(n => n.Key).Where(scope.HoldsNode).Select(k => k.Value).Concat(part.Anchors).ToHashSet(StringComparer.Ordinal);
        int before;
        do
        {
            before = inside.Count;
            foreach (var edge in graph.Edges.Where(e => e.Type is EdgeType.PartOf or EdgeType.On && e.From.Kind is NodeKind.Surface or NodeKind.Action && inside.Contains(e.To.Value)))
            {
                _ = inside.Add(edge.From.Value);
            }
        }
        while (inside.Count != before);

        return inside;
    }

    private static bool Keeps(PackCandidate candidate, HashSet<string> inside, GraphSnapshot graph)
    {
        if (candidate.Item.Node is not { } node || !NodeKey.TryFrom(node, out var key))
        {
            return false;
        }

        if (key.Kind == NodeKind.Concept)
        {
            return true;   // another pin is a fine edge end: the lens still shows that billing depends on auth
        }

        if (key.Kind == NodeKind.Thing)
        {
            return graph.Edges.Any(e => e.Type is EdgeType.Reads or EdgeType.Writes && string.Equals(e.To.Value, node, StringComparison.Ordinal) && inside.Contains(e.From.Value));
        }

        return key.Kind == NodeKind.Decision
            ? graph.Edges.Any(e => e.Type == EdgeType.Decides && string.Equals(e.From.Value, node, StringComparison.Ordinal) && inside.Contains(e.To.Value))
            : inside.Contains(node);
    }
}
