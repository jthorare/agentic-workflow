namespace Aw.Context.Core.Graph;

/// <summary>The tag features of a graph: features a model named across files that have a home elsewhere. They hold no file ('implements') of their own, only 'tags' links, so they are never an area, a concept member or a neighbor (spec 2026-10-27 §2.3).</summary>
internal static class TagFeatures
{
    /// <summary>The keys of the features that have a 'tags' link from a file and no 'implements' link.</summary>
    /// <param name="graph">The graph.</param>
    /// <returns>The keys (ordinal).</returns>
    internal static HashSet<string> Of(GraphSnapshot graph)
    {
        var tagged = graph.Edges.Where(e => e.Type == EdgeType.Tags).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
        if (tagged.Count == 0)
        {
            return tagged;
        }

        tagged.ExceptWith(graph.Edges.Where(e => e.Type == EdgeType.Implements).Select(e => e.To.Value));
        return tagged;
    }
}
