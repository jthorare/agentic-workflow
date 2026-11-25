using System.Text;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What a succeeded run gives as its result (#3807): the output of the node its spec's 'result' names, or of the nodes no edge leaves, keyed by node id; a collect gives its own output, never its item rows (#4093).
/// The result is kept up to 64 KB on its own: a cut node or a result over the cap becomes a <see cref="RunResultMarker"/>, never null.
/// </summary>
internal static class GraphResultOutputs
{
    /// <summary>The run's outputs as JSON text.</summary>
    /// <param name="spec">The spec the run walked.</param>
    /// <param name="rows">The run's node rows.</param>
    /// <returns>The text, or null when no result node succeeded.</returns>
    internal static string? Build(GraphSpec spec, IReadOnlyList<NodeRun> rows)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(rows);
        var result = new JsonObject();
        IReadOnlyList<NodeId> picks = spec.Result is { } named ? [named] : Last(spec);
        foreach (var node in picks)
        {
            var mine = Kept([.. rows.Where(r => r.NodeId == node)]);
            if (mine.Count > 0)
            {
                result[node.Value] = Slot(mine);
            }
        }

        if (result.Count == 0)
        {
            return null;
        }

        var text = result.ToJsonString();
        var bytes = Encoding.UTF8.GetByteCount(text);
        // why: a cut JSON text no longer parses, so a result over the cap is one marker with its size, which a reader refuses with SND-GRAPH-108
        return bytes <= NodeRunLimits.PayloadBytes ? text : RunResultMarker.Of(bytes).ToJsonString();
    }

    // why: "the last node" read from the graph, not from timing: every node no edge leaves, in spec order
    private static List<NodeId> Last(GraphSpec spec)
    {
        var left = spec.Edges.Select(static e => e.From).ToHashSet();
        return [.. spec.Nodes.Where(n => !left.Contains(n.Id)).Select(static n => n.Id)];
    }

    // why: a collect has its own row at item 0 beside one row per item (#4093): its output is that row alone (items, count, skipped), and the item rows are detail of the run page.
    // The row at 0 is picked before the status filter, so a failed collect that 'continue' let pass gives nothing, never its item rows. A for_each body node has rows at 1..N only.
    private static List<NodeRun> Kept(List<NodeRun> mine)
    {
        var own = mine.FindAll(static r => r.ItemIndex == 0);
        return [.. (own.Count > 0 ? own : mine).Where(static r => r.Status == NodeRunStatus.Succeeded && r.Output is not null).OrderBy(static r => r.ItemIndex)];
    }

    // why: a node that ran once gives its outputs object; a for_each body node gives the list in item order; a cut row makes the whole slot a marker
    private static JsonNode? Slot(List<NodeRun> rows)
    {
        if (rows.FirstOrDefault(static r => r.Truncated) is { } cut)
        {
            return RunResultMarker.Of(Size(cut.Output));
        }

        var parsed = rows.Select(static r => StoredJson.Parse(r.Output)).ToList();
        return rows.Exists(static r => r.ItemIndex > 0) ? new JsonArray([.. parsed]) : parsed[0];
    }

    private static long? Size(string? output) =>
        StoredJson.Parse(output) is JsonObject envelope && envelope["bytes"] is JsonValue value && value.TryGetValue<long>(out var bytes) ? bytes : null;
}
