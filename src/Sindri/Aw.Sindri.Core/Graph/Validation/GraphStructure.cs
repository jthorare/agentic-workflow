namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>The shape of a graph as the validator reads it: node order, the edges that end at real nodes, and who is upstream of whom.</summary>
internal sealed class GraphStructure
{
    private readonly Dictionary<string, List<string>> _next = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _previous = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.Ordinal);
    private Dictionary<string, HashSet<string>>? _dominators;

    public GraphStructure(GraphSpec spec)
    {
        Order = [.. spec.Nodes.Select(static n => n.Id.Value)];
        foreach (var id in Order)
        {
            _next[id] = [];
            _previous[id] = [];
        }

        foreach (var node in spec.Nodes)
        {
            _ = _nodes.TryAdd(node.Id.Value, node);
        }

        foreach (var edge in spec.Edges)
        {
            if (_next.TryGetValue(edge.From.Value, out var following) && _previous.TryGetValue(edge.To.Value, out var preceding))
            {
                following.Add(edge.To.Value);
                preceding.Add(edge.From.Value);
            }
        }
    }

    /// <summary>The node ids in spec order (a repeated id is listed once per node).</summary>
    public List<string> Order { get; }

    /// <summary>The first node with <paramref name="id"/>, or null.</summary>
    public GraphNode? Find(string id) => _nodes.GetValueOrDefault(id);

    public bool Has(string id) => _next.ContainsKey(id);

    /// <summary>The nodes reachable from <paramref name="starts"/> by edges, the starts included.</summary>
    public HashSet<string> Reach(IEnumerable<string> starts) => Walk(starts, _next);

    /// <summary>The nodes with a path to <paramref name="id"/>, not counting <paramref name="id"/> itself unless it is on a cycle.</summary>
    public HashSet<string> Upstream(string id) => Walk(_previous[id], _previous);

    /// <summary>
    /// The nodes that lie on every path from a start node (one with no incoming edge) to <paramref name="id"/>, <paramref name="id"/> included:
    /// dom(n) = {n} + the intersection of dom(p) over the nodes p that feed n. Computed once per graph; settles on a cycle too.
    /// </summary>
    public HashSet<string> Dominators(string id)
    {
        _dominators ??= ComputeDominators();
        return _dominators.TryGetValue(id, out var found) ? found : [id];
    }

    private Dictionary<string, HashSet<string>> ComputeDominators()
    {
        var ids = Order.Distinct(StringComparer.Ordinal).ToList();
        var all = new HashSet<string>(ids, StringComparer.Ordinal);
        var dom = ids.ToDictionary(static i => i, i => _previous[i].Count == 0 ? new HashSet<string>([i], StringComparer.Ordinal) : new HashSet<string>(all, StringComparer.Ordinal), StringComparer.Ordinal);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var id in ids.Where(i => _previous[i].Count > 0))
            {
                var next = new HashSet<string>(dom[_previous[id][0]], StringComparer.Ordinal);
                foreach (var before in _previous[id].Skip(1))
                {
                    next.IntersectWith(dom[before]);
                }

                _ = next.Add(id);
                if (next.Count != dom[id].Count)
                {
                    dom[id] = next;
                    changed = true;
                }
            }
        }

        return dom;
    }

    /// <summary>Every cycle, each as its nodes in spec order of the first one, found by walking nodes and edges in spec order.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Cycles()
    {
        var found = new List<IReadOnlyList<string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);
        var path = new List<string>();
        foreach (var id in Order.Distinct(StringComparer.Ordinal))
        {
            Visit(id, path, (seen, done), found);
        }

        return found;
    }

    private void Visit(string id, List<string> path, (HashSet<string> Open, HashSet<string> Done) marks, List<IReadOnlyList<string>> found)
    {
        if (marks.Done.Contains(id))
        {
            return;
        }

        if (!marks.Open.Add(id))
        {
            var cycle = path.Skip(path.IndexOf(id)).ToList();
            var first = 0;
            for (var i = 1; i < cycle.Count; i++)
            {
                if (Order.IndexOf(cycle[i]) < Order.IndexOf(cycle[first]))
                {
                    first = i;
                }
            }

            found.Add([.. cycle.Skip(first), .. cycle.Take(first)]);
            return;
        }

        path.Add(id);
        foreach (var next in _next[id])
        {
            Visit(next, path, marks, found);
        }

        path.RemoveAt(path.Count - 1);
        _ = marks.Open.Remove(id);
        _ = marks.Done.Add(id);
    }

    private static HashSet<string> Walk(IEnumerable<string> starts, Dictionary<string, List<string>> edges)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(starts);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (reached.Add(id))
            {
                foreach (var next in edges[id])
                {
                    queue.Enqueue(next);
                }
            }
        }

        return reached;
    }
}
