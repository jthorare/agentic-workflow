using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.WholeMap;

/// <summary>A graph written as keys and edges, for the area rules.</summary>
internal sealed class AreaGraph
{
    private readonly List<GraphNode> _nodes = [];
    private readonly List<GraphEdge> _edges = [];

    public AreaGraph Node(params string[] keys)
    {
        _nodes.AddRange(keys.Select(k => new GraphNode(NodeKey.From(k), k)));
        return this;
    }

    public AreaGraph Edge(string from, EdgeType type, string to)
    {
        _edges.Add(new GraphEdge(NodeKey.From(from), NodeKey.From(to), type));
        return this;
    }

    /// <summary>The graph. Unless <paramref name="padded"/> is false it also holds a dozen lone features, so a rule under test is not in a map small enough for the adaptive depth to act.</summary>
    public GraphSnapshot Build(bool reversed = false, bool padded = true)
    {
        var nodes = padded ? [.. _nodes, .. Enumerable.Range(0, 12).Select(i => new GraphNode(NodeKey.From("feature:zpad" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)), "pad"))] : _nodes;
        return new(reversed ? [.. nodes.AsEnumerable().Reverse()] : nodes, reversed ? [.. _edges.AsEnumerable().Reverse()] : _edges, [], []);
    }
}
