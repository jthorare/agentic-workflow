namespace Aw.Sindri.Core.Graph.Layout;

/// <summary>Sindri's own layered layout of a workflow graph; agents never send positions.</summary>
public static class GraphLayout
{
    /// <summary>Canvas units between two layers.</summary>
    public const int LayerWidth = 280;

    /// <summary>Canvas units between two rows.</summary>
    public const int RowHeight = 140;

    /// <summary>
    /// Puts each node in the layer of the longest path that reaches it from a node nothing feeds (a trigger, in a valid graph); nodes of one layer
    /// follow the spec's node order. A graph with a loop still gets positions.
    /// </summary>
    /// <param name="spec">The graph.</param>
    /// <returns>A position per node id.</returns>
    public static IReadOnlyDictionary<string, GraphPosition> Compute(GraphSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var ids = spec.Nodes.Select(static n => n.Id.Value).Distinct(StringComparer.Ordinal).ToList();
        var layer = ids.ToDictionary(static id => id, static _ => 0, StringComparer.Ordinal);
        var edges = spec.Edges.Where(e => layer.ContainsKey(e.From.Value) && layer.ContainsKey(e.To.Value) && e.From.Value != e.To.Value).ToList();

        // why: longest path by repeated relaxation; a loop would raise layers forever, so n passes at most and a cap of n - 1.
        for (var pass = 0; pass < ids.Count; pass++)
        {
            var changed = false;
            foreach (var edge in edges)
            {
                var next = Math.Min(layer[edge.From.Value] + 1, ids.Count - 1);
                if (next > layer[edge.To.Value])
                {
                    layer[edge.To.Value] = next;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        var rows = new Dictionary<int, int>();
        var result = new Dictionary<string, GraphPosition>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var l = layer[id];
            var row = rows.GetValueOrDefault(l);
            rows[l] = row + 1;
            result[id] = new GraphPosition(l, row, l * LayerWidth, row * RowHeight);
        }

        return result;
    }
}
