namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The shortest path from the nearest starting point to a node, over the map's edges walked in either direction (the inspector's "why it is in this map"). Plain C#.</summary>
internal static class MapPath
{
    /// <summary>Finds the shortest path.</summary>
    /// <param name="links">The map's edges.</param>
    /// <param name="starts">The starting points, in order; a tie goes to the first.</param>
    /// <param name="target">The node to reach.</param>
    /// <returns>The path, or null when no starting point reaches the node.</returns>
    internal static MapRoute? Find(IEnumerable<MapLink> links, IEnumerable<string> starts, string target)
    {
        var (came, seen) = Walk(links, starts);
        if (!seen.Contains(target))
        {
            return null;
        }

        var hops = new List<MapPathHop>();
        var here = target;
        while (came.TryGetValue(here, out var step))
        {
            hops.Add(new MapPathHop(here, step.Type));
            here = step.Prev;
        }

        hops.Reverse();
        return new MapRoute(here, hops);
    }

    // A breadth-first walk from all the starts at once; came[x] is the node x was first reached from.
    private static (Dictionary<string, (string Prev, string Type)> Came, HashSet<string> Seen) Walk(IEnumerable<MapLink> links, IEnumerable<string> starts)
    {
        var next = new Dictionary<string, List<(string To, string Type)>>(StringComparer.Ordinal);
        foreach (var link in links)
        {
            Add(next, link.From, link.To, link.Type);
            Add(next, link.To, link.From, link.Type);
        }

        var came = new Dictionary<string, (string Prev, string Type)>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        foreach (var start in starts.Where(seen.Add))
        {
            queue.Enqueue(start);
        }

        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            foreach (var (to, type) in next.GetValueOrDefault(at) ?? [])
            {
                if (seen.Add(to))
                {
                    came[to] = (at, type);
                    queue.Enqueue(to);
                }
            }
        }

        return (came, seen);
    }

    /// <summary>The path in plain words.</summary>
    /// <param name="label">A node key's label.</param>
    /// <param name="route">The path, or null when none reaches the node.</param>
    /// <returns>The sentence.</returns>
    internal static string Sentence(Func<string, string> label, MapRoute? route) => route switch
    {
        null => Labels.ContextMapPathNone,
        { Hops.Count: 0 } => Labels.ContextMapPathAnchor,
        _ => Labels.ContextMapPathSentence(label(route.Start), route.Hops.Select(h => Labels.ContextMapPathHop(label(h.Key), Labels.ContextMapEdgeWord(h.Type)))),
    };

    private static void Add(Dictionary<string, List<(string To, string Type)>> next, string from, string to, string type)
    {
        if (!next.TryGetValue(from, out var list))
        {
            next[from] = list = [];
        }

        list.Add((to, type));
    }
}
