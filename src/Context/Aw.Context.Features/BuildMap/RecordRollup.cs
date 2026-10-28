using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.BuildMap;

/// <summary>The edges of the records over the file cap (spec 2026-10-27 §7.5): one 'mentions' edge per home feature, counting the record's files in it, and the record's node.</summary>
internal static class RecordRollup
{
    /// <summary>Rolls each record's files up to the features that are their homes.</summary>
    /// <param name="overflow">The records over the cap.</param>
    /// <param name="map">The joined extraction, home 'implements' edges included.</param>
    /// <returns>The nodes and edges to add; a record none of whose files has a home adds nothing.</returns>
    internal static ExtractedMap Edges(IReadOnlyList<RolledRecord> overflow, ExtractedMap map)
    {
        ArgumentNullException.ThrowIfNull(overflow);
        ArgumentNullException.ThrowIfNull(map);
        var homes = new Dictionary<string, NodeKey>(StringComparer.Ordinal);
        foreach (var edge in map.Edges.Where(e => e is { Type: EdgeType.Implements, Provenance: Provenance.Deterministic, From.Kind: NodeKind.Code, To.Kind: NodeKind.Feature }))
        {
            homes[edge.From.Value] = edge.To;
        }

        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        foreach (var record in overflow)
        {
            var perFeature = record.Paths.Select(p => homes.GetValueOrDefault("code:" + p)).OfType<NodeKey>()
                .GroupBy(f => f.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
            if (perFeature.Count > 0)
            {
                nodes.Add(record.Node);
            }

            edges.AddRange(perFeature.Select(g => new GraphEdge(record.Node.Key, g.First(), EdgeType.Mentions, Provenance.Deterministic, 1.0, [new EvidenceRef(record.Evidence)], Count: g.Count())));
        }

        return new ExtractedMap(nodes, edges);
    }
}
