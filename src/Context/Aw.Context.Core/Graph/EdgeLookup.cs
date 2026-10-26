using System.Runtime.CompilerServices;

namespace Aw.Context.Core.Graph;

/// <summary>A graph's edges by type and by the node they end in, each in edge-list order, so a typed lookup never scans every edge.</summary>
internal sealed class EdgeLookup
{
    private static readonly IReadOnlyList<GraphEdge> _none = [];

    // One lookup per graph instance: a graph is immutable and the process keeps the same instance per map version.
    private static readonly ConditionalWeakTable<GraphSnapshot, EdgeLookup> _all = [];
    private readonly Dictionary<EdgeType, List<GraphEdge>> _byType = [];
    private readonly Dictionary<string, List<(int Position, GraphEdge Edge)>> _byTarget = new(StringComparer.Ordinal);

    private EdgeLookup(GraphSnapshot graph)
    {
        for (var position = 0; position < graph.Edges.Count; position++)
        {
            var edge = graph.Edges[position];
            if (!_byType.TryGetValue(edge.Type, out var typed))
            {
                _byType[edge.Type] = typed = [];
            }

            typed.Add(edge);
            if (!_byTarget.TryGetValue(edge.To.Value, out var targeted))
            {
                _byTarget[edge.To.Value] = targeted = [];
            }

            targeted.Add((position, edge));
        }
    }

    /// <summary>The lookup of a graph, built once per graph instance.</summary>
    /// <param name="graph">The graph.</param>
    internal static EdgeLookup For(GraphSnapshot graph) => _all.GetValue(graph, static g => new EdgeLookup(g));

    /// <summary>The edges of one type, in edge-list order.</summary>
    internal IReadOnlyList<GraphEdge> OfType(EdgeType type) => _byType.TryGetValue(type, out var edges) ? edges : _none;

    /// <summary>The edges of the given types that end in one of <paramref name="targets"/>, in edge-list order.</summary>
    internal List<GraphEdge> Into(HashSet<string> targets, params EdgeType[] types)
    {
        var found = new List<(int Position, GraphEdge Edge)>();
        foreach (var target in targets)
        {
            if (_byTarget.TryGetValue(target, out var edges))
            {
                found.AddRange(edges.Where(e => types.Contains(e.Edge.Type)));
            }
        }

        found.Sort(static (a, b) => a.Position.CompareTo(b.Position));
        return [.. found.Select(f => f.Edge)];
    }
}
