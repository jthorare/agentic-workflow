namespace Aw.Context.Core.Graph;

/// <summary>A graph's edges by their ends, for walks; every list is in a fixed order (other end's key, then type), so walks are deterministic.</summary>
internal sealed class GraphIndex
{
    private static readonly IReadOnlyList<GraphEdge> _none = [];
    private static long _builds;

    // One index per graph instance: a graph is immutable and the process keeps the same instance per map version, so a walk never re-indexes every edge.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GraphSnapshot, GraphIndex> _all = [];
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GraphSnapshot, System.Collections.Concurrent.ConcurrentDictionary<string, GraphIndex>> _structures = [];
    private readonly Dictionary<string, List<GraphEdge>> _out = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<GraphEdge>> _in = new(StringComparer.Ordinal);

    internal GraphIndex(GraphSnapshot graph)
    {
        _ = Interlocked.Increment(ref _builds);
        Graph = graph;
        Nodes = graph.Nodes.ToDictionary(n => n.Key.Value, StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            Add(_out, edge.From.Value, edge);
            Add(_in, edge.To.Value, edge);
        }

        foreach (var list in _out.Values)
        {
            list.Sort((a, b) => Compare(a.To, a.Type, b.To, b.Type));
        }

        foreach (var list in _in.Values)
        {
            list.Sort((a, b) => Compare(a.From, a.Type, b.From, b.Type));
        }
    }

    /// <summary>The index of every edge in a graph, built once per graph instance.</summary>
    /// <param name="graph">The graph.</param>
    internal static GraphIndex For(GraphSnapshot graph) => _all.GetValue(graph, static g => new GraphIndex(g));

    /// <summary>The index of only the edges of <paramref name="types"/>, built once per graph instance and type set.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="types">The edge types kept.</param>
    internal static GraphIndex Of(GraphSnapshot graph, IReadOnlyList<EdgeType> types)
    {
        var byTypes = _structures.GetOrCreateValue(graph);
        var key = string.Join(',', types.Cast<int>().Order());
        return byTypes.GetOrAdd(key, static (_, state) => new GraphIndex(state.Graph with { Edges = [.. state.Graph.Edges.Where(e => state.Types.Contains(e.Type))] }), (Graph: graph, Types: types));
    }

    /// <summary>Test seam: how many indexes this process has built.</summary>
    internal static long Builds => Interlocked.Read(ref _builds);

    internal GraphSnapshot Graph { get; }

    internal Dictionary<string, GraphNode> Nodes { get; }

    /// <summary>The edges leaving <paramref name="key"/>, optionally of one type.</summary>
    internal IEnumerable<GraphEdge> Out(NodeKey key, EdgeType? type = null) => Of(_out, key, type);

    /// <summary>The edges reaching <paramref name="key"/>, optionally of one type.</summary>
    internal IEnumerable<GraphEdge> In(NodeKey key, EdgeType? type = null) => Of(_in, key, type);

    private static IEnumerable<GraphEdge> Of(Dictionary<string, List<GraphEdge>> side, NodeKey key, EdgeType? type)
    {
        var edges = side.TryGetValue(key.Value, out var list) ? list : _none;
        return type is { } t ? edges.Where(e => e.Type == t) : edges;
    }

    private static void Add(Dictionary<string, List<GraphEdge>> side, string key, GraphEdge edge)
    {
        if (!side.TryGetValue(key, out var list))
        {
            side[key] = list = [];
        }

        list.Add(edge);
    }

    private static int Compare(NodeKey a, EdgeType at, NodeKey b, EdgeType bt)
    {
        var byKey = string.CompareOrdinal(a.Value, b.Value);
        return byKey != 0 ? byKey : at.CompareTo(bt);
    }
}
