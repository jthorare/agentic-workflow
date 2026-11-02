using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Concepts;

/// <summary>A pin's 'dependsOn' and 'owns' as Human edges (spec 2026-10-27 §10.3), made fresh on every build.</summary>
public static class PinEdges
{
    /// <summary>The declared edges whose endpoints the map has.</summary>
    /// <param name="graph">The map, concepts and things included.</param>
    /// <param name="pins">Every pin of the build.</param>
    /// <returns>The edges and the skipped count.</returns>
    public static DeclaredEdges Declared(GraphSnapshot graph, IReadOnlyList<ConceptPin> pins)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pins);
        var nodes = graph.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var edges = new List<GraphEdge>();
        var skipped = 0;
        foreach (var (id, pin) in PinIds.Resolve(pins))
        {
            var from = NodeKey.From("concept:" + id);
            skipped += pin.DependsOn.Count(other => !Add(from, EdgeType.DependsOn, ToConcept(other), $"profile:index.concepts.{id}.dependsOn"));
            skipped += pin.Owns.Count(store => !Add(from, EdgeType.Writes, DataStorePass.ThingKey(store), $"profile:index.concepts.{id}.owns"));
        }

        return new DeclaredEdges([.. edges.DistinctBy(e => (e.From.Value, e.To.Value, e.Type))], skipped);

        bool Add(NodeKey from, EdgeType type, NodeKey? to, string evidence)
        {
            if (to is not { } target || !nodes.Contains(from.Value) || !nodes.Contains(target.Value) || !RecordKey.TryFrom(evidence, out var record))
            {
                return false;
            }

            edges.Add(new GraphEdge(from, target, type, Provenance.Human, 1.0, [new EvidenceRef(record)]));
            return true;
        }
    }

    private static NodeKey? ToConcept(string id) => NodeKey.TryFrom("concept:" + id, out var key) ? key : null;
}
