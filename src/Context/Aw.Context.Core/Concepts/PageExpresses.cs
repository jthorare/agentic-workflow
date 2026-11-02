using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Concepts;

/// <summary>The suggested 'expresses' edges of a map build: a page or an action expresses each concept its implementing code's home features belong to (pages spec §3.2).</summary>
public static class PageExpresses
{
    /// <summary>The most concepts one page or action is suggested to express.</summary>
    public const int MostPerSource = 3;

    /// <summary>Adds the suggested edges to a graph that already has its concepts.</summary>
    /// <param name="graph">The graph after concept naming and the pins.</param>
    /// <returns>The graph with its suggested edges: per action or page, up to three concepts, the ones with the most implementing files first, then by id.</returns>
    public static GraphSnapshot Apply(GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var concepts = graph.Edges.Where(e => e is { Type: EdgeType.BelongsTo, From.Kind: NodeKind.Feature, To.Kind: NodeKind.Concept })
            .ToLookup(e => e.From.Value, e => e.To.Value, StringComparer.Ordinal);
        var homes = graph.Edges.Where(e => e is { Type: EdgeType.Implements, From.Kind: NodeKind.Code, To.Kind: NodeKind.Feature })
            .ToLookup(e => e.From.Value, e => e.To.Value, StringComparer.Ordinal);
        var files = graph.Edges.Where(e => e is { Type: EdgeType.Implements, From.Kind: NodeKind.Code } && e.To.Kind is NodeKind.Action or NodeKind.Surface)
            .ToLookup(e => e.To.Value, e => e.From.Value, StringComparer.Ordinal);
        var edges = graph.Edges.Where(e => e.Type == EdgeType.Implements).ToList();
        var nodeByKey = graph.Nodes.GroupBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var existing = graph.Edges.Where(e => e.Type == EdgeType.Expresses).Select(e => (e.From.Value, e.To.Value)).ToHashSet();
        var made = new List<GraphEdge>();
        foreach (var node in graph.Nodes.Where(n => n.Key.Kind == NodeKind.Action || (n.Key.Kind == NodeKind.Surface && PageKinds.IsPage(n, edges))).OrderBy(n => n.Key.Value, StringComparer.Ordinal))
        {
            var counts = files[node.Key.Value].Distinct(StringComparer.Ordinal)
                .SelectMany(file => homes[file].SelectMany(feature => concepts[feature]).Distinct(StringComparer.Ordinal))
                .GroupBy(c => c, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Take(MostPerSource);
            foreach (var concept in counts)
            {
                if (nodeByKey.ContainsKey(concept.Key) && !existing.Contains((node.Key.Value, concept.Key)))
                {
                    made.Add(new GraphEdge(node.Key, NodeKey.From(concept.Key), EdgeType.Expresses, Provenance.Deterministic));
                }
            }
        }

        return made.Count == 0 ? graph : graph with { Edges = [.. graph.Edges, .. made] };
    }
}
