using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Which steps a re-run from a step takes from the source run and which it runs again (Workflows page spec §7.3, P7). Pure: the service and the walk's restore activity share it.</summary>
public static class RerunPlan
{
    /// <summary>The node and every node reachable from it by edges, in spec order.</summary>
    /// <param name="spec">The graph.</param>
    /// <param name="from">The chosen node.</param>
    /// <returns>The node ids.</returns>
    public static IReadOnlyList<NodeId> FromHere(GraphSpec spec, NodeId from) => Reach(spec, from, forward: true);

    /// <summary>Every node the chosen node reads from, directly or not (not the node itself), in spec order.</summary>
    /// <param name="spec">The graph.</param>
    /// <param name="node">The chosen node.</param>
    /// <returns>The node ids.</returns>
    public static IReadOnlyList<NodeId> Before(GraphSpec spec, NodeId node) => [.. Reach(spec, node, forward: false).Where(n => n != node)];

    /// <summary>
    /// The steps a re-run from <paramref name="from"/> takes from the source run, in spec order: every step not after the chosen one whose steps before it are taken too, and that succeeded
    /// or was skipped on a branch nobody took. A step the source skipped only because a failure or a cancel halted the walk runs again, and a loop is taken whole or runs again whole.
    /// </summary>
    /// <param name="spec">The source run's graph.</param>
    /// <param name="from">The chosen node.</param>
    /// <param name="rows">The source run's node runs.</param>
    /// <returns>The node ids.</returns>
    public static IReadOnlyList<NodeId> Restored(GraphSpec spec, NodeId from, IReadOnlyList<NodeRun> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new RerunRestore(spec, FromHere(spec, from), rows).Steps();
    }

    /// <summary>The branches a restored node took: the outputs of its edges into steps that were not skipped in the source run. A step with no row counts as not skipped.</summary>
    /// <param name="spec">The graph.</param>
    /// <param name="node">The restored node.</param>
    /// <param name="skipped">The nodes the source run skipped.</param>
    /// <returns>The branch names ('out' for an edge with no named output).</returns>
    public static IReadOnlyList<string> Taken(GraphSpec spec, NodeId node, IReadOnlySet<NodeId> skipped)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(skipped);
        return [.. spec.Edges.Where(e => e.From == node && !skipped.Contains(e.To)).Select(static e => e.Output ?? "out").Distinct(StringComparer.Ordinal)];
    }

    private static List<NodeId> Reach(GraphSpec spec, NodeId start, bool forward)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var seen = new HashSet<NodeId> { start };
        var queue = new Queue<NodeId>([start]);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in spec.Edges.Where(e => (forward ? e.From : e.To) == current).Select(e => forward ? e.To : e.From).Where(seen.Add))
            {
                queue.Enqueue(next);
            }
        }

        return [.. spec.Nodes.Select(static n => n.Id).Where(seen.Contains)];
    }
}
