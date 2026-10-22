namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// What stays lit on the map while a node is hovered or selected (F3 spec 1 §2, "Hover rule"): the nodes and the edges around it.
/// Everything else dims. A cite lights the keys it names and the edges among them. Plain C#.
/// </summary>
internal sealed class MapHighlight
{
    private MapHighlight(IReadOnlySet<string> nodes, IReadOnlySet<string> edges)
    {
        Nodes = nodes;
        Edges = edges;
    }

    /// <summary>Gets the nodes that stay lit.</summary>
    internal IReadOnlySet<string> Nodes { get; }

    /// <summary>Gets the edges that stay lit.</summary>
    internal IReadOnlySet<string> Edges { get; }

    /// <summary>The keys, their neighbours, and the edges between them.</summary>
    /// <param name="links">The map's edges.</param>
    /// <param name="keys">The hovered or selected node keys.</param>
    /// <returns>The highlight.</returns>
    internal static MapHighlight Around(IEnumerable<MapLink> links, IEnumerable<string> keys)
    {
        var chosen = new HashSet<string>(keys, StringComparer.Ordinal);
        var nodes = new HashSet<string>(chosen, StringComparer.Ordinal);
        var edges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in links.Where(l => chosen.Contains(l.From) || chosen.Contains(l.To)))
        {
            _ = edges.Add(link.Key);
            _ = nodes.Add(link.From);
            _ = nodes.Add(link.To);
        }

        return new MapHighlight(nodes, edges);
    }

    /// <summary>Only the keys, and the edges that join two of them.</summary>
    /// <param name="links">The map's edges.</param>
    /// <param name="keys">The cited node keys.</param>
    /// <returns>The highlight.</returns>
    internal static MapHighlight Among(IEnumerable<MapLink> links, IEnumerable<string> keys)
    {
        var chosen = new HashSet<string>(keys, StringComparer.Ordinal);
        var edges = links.Where(l => chosen.Contains(l.From) && chosen.Contains(l.To)).Select(l => l.Key).ToHashSet(StringComparer.Ordinal);
        return new MapHighlight(chosen, edges);
    }

    /// <summary>Whether a node dims.</summary>
    /// <param name="key">The node's key.</param>
    /// <returns>True when it is outside the lit set.</returns>
    internal bool Dims(string key) => !Nodes.Contains(key);

    /// <summary>Whether an edge dims.</summary>
    /// <param name="key">The edge's key.</param>
    /// <returns>True when it is outside the lit set.</returns>
    internal bool DimsEdge(string key) => !Edges.Contains(key);
}
