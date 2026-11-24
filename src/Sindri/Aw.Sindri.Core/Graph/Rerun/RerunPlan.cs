using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Core.Graph.Rerun;

/// <summary>Which steps a re-run from a step takes from the source run and which it runs again (Workflows page spec §7.3, P7). Pure: the service and the walk's restore activity share it, so both read a taken step's branch from the same stored row.</summary>
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

    /// <summary>
    /// The branches a step took, read from its own rows in the source run: none when no row succeeded, 'true' or 'false' from a condition's stored result,
    /// 'answered' or 'timeout' from an ask_owner's stored answer, and 'out' for every other step. Null when a branching step's row does not say.
    /// </summary>
    /// <param name="node">The step.</param>
    /// <param name="rows">Its rows.</param>
    /// <returns>The branch names, or null.</returns>
    public static IReadOnlyList<string>? Branches(GraphNode node, IReadOnlyList<NodeRun> rows)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(rows);
        return RerunBranches.Of(node, rows);
    }

    /// <summary>The branches a restored step took, plus its edges into the chosen step: a re-run from a step the source skipped on a branch it did not take runs that step on purpose.</summary>
    /// <param name="spec">The graph.</param>
    /// <param name="node">The restored step.</param>
    /// <param name="branches">The branches its row says it took (<see cref="Branches"/>).</param>
    /// <param name="from">The step the re-run starts from.</param>
    /// <returns>The branch names.</returns>
    public static IReadOnlyList<string> WithChosen(GraphSpec spec, NodeId node, IReadOnlyList<string> branches, NodeId from)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(branches);
        return [.. branches.Concat(spec.Edges.Where(e => e.From == node && e.To == from).Select(static e => e.Output ?? "out")).Distinct(StringComparer.Ordinal)];
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
