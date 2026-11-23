using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.GetWorkflowRun;

/// <summary>
/// The order a run's node runs are served in: the graph's order, the one the walk runs them in (a node comes after every node with an edge into it, ties by the spec's node order),
/// then each node's items by index. The store keeps rows by node id, so without this a straight start -> measure -> check -> tell run read "check, measure, start, tell" (#3307).
/// </summary>
internal static class RunOrder
{
    /// <summary>The rows in graph order. A row whose node the spec lacks, or every row when there is no spec, keeps the stored order after the placed ones.</summary>
    /// <param name="spec">The revision the run walked, or null when the store can't give it.</param>
    /// <param name="rows">The rows as stored.</param>
    /// <returns>The rows to serve.</returns>
    public static IReadOnlyList<NodeRun> Of(GraphSpec? spec, IReadOnlyList<NodeRun> rows)
    {
        if (spec is null)
        {
            return rows;
        }

        var rank = Ranks(spec);
        return [.. rows
            .Select(static (row, stored) => (Row: row, Stored: stored))
            .OrderBy(p => rank.TryGetValue(p.Row.NodeId, out var r) ? r : int.MaxValue)
            .ThenBy(p => rank.ContainsKey(p.Row.NodeId) ? p.Row.ItemIndex : 0)
            .ThenBy(static p => p.Stored)
            .Select(static p => p.Row)];
    }

    // why: the same rule as the walk (GraphWalkRules.Ready, first ready node in spec order), so the list reads like the run went. A cycle a valid spec can't have still ranks: its nodes follow in spec order.
    private static Dictionary<NodeId, int> Ranks(GraphSpec spec)
    {
        var ids = spec.Nodes.Select(static n => n.Id).Distinct().ToList();
        var known = ids.ToHashSet();
        var edges = spec.Edges.Where(e => known.Contains(e.From) && known.Contains(e.To) && e.From != e.To).ToList();
        Dictionary<NodeId, int> rank = [];
        List<NodeId> pending = [.. ids];
        while (pending.Count > 0)
        {
            var ready = pending.FindIndex(id => edges.Where(e => e.To == id).All(e => rank.ContainsKey(e.From)));
            var at = ready < 0 ? 0 : ready;
            rank[pending[at]] = rank.Count;
            pending.RemoveAt(at);
        }

        return rank;
    }
}
