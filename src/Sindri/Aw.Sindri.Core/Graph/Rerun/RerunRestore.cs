using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Loops;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Core.Graph.Rerun;

/// <summary>
/// Decides which steps of the source run a re-run takes (<see cref="RerunPlan.Restored"/>), each step after the steps it reads from. A step is taken when it is not after the chosen step,
/// every step it reads from is taken, and its rows say it is done: it succeeded and its row says which branch it took, or it was skipped on a branch the taken steps did not take. Every other step runs again.
/// </summary>
internal sealed class RerunRestore
{
    private const string Out = "out";

    private readonly GraphSpec _spec;
    private readonly HashSet<NodeId> _again;
    private readonly ILookup<NodeId, NodeRun> _rows;
    private readonly Dictionary<NodeId, GraphNode> _nodes;
    private readonly Dictionary<NodeId, IReadOnlyList<string>> _branches = [];
    private readonly Dictionary<NodeId, LoopShape> _loops = [];
    private readonly Dictionary<NodeId, NodeId> _loopOf = [];
    private readonly Dictionary<NodeId, Keep> _kept = [];

    internal RerunRestore(GraphSpec spec, IReadOnlyList<NodeId> again, IReadOnlyList<NodeRun> rows)
    {
        _spec = spec;
        _again = [.. again];
        _rows = rows.ToLookup(static r => r.NodeId);
        _nodes = spec.Nodes.ToDictionary(static n => n.Id);
        foreach (var node in spec.Nodes.Where(static n => string.Equals(n.Type, LoopBody.ForEachType, StringComparison.Ordinal)))
        {
            if (LoopBody.Find(spec, node.Id) is { IsSuccess: true } loop)
            {
                _loops[node.Id] = loop.Value;
                foreach (var repeated in loop.Value.PerItem)
                {
                    _loopOf[repeated.Id] = node.Id;
                }
            }
        }
    }

    private enum Keep
    {
        Run,
        Ran,
        Skipped,
    }

    /// <summary>The steps taken from the source run, in spec order.</summary>
    /// <returns>The node ids.</returns>
    internal List<NodeId> Steps() => [.. _spec.Nodes.Select(static n => n.Id).Where(id => Decide(id) != Keep.Run)];

    private Keep Decide(NodeId id)
    {
        if (!_kept.TryGetValue(id, out var keep))
        {
            keep = Judge(id);
            _kept[id] = keep;
        }

        return keep;
    }

    private Keep Judge(NodeId id)
    {
        if (_again.Contains(id) || Into(id).Exists(e => Decide(e.From) == Keep.Run))
        {
            return Keep.Run;
        }

        // why: a body step or collect ran inside its loop; it is taken with its for_each, even with no row (a loop of no items), so the walk never runs it once as a plain step
        return _loopOf.TryGetValue(id, out var forEach) ? Repeated(id, forEach) : Own(id);
    }

    private Keep Repeated(NodeId id, NodeId forEach)
    {
        if (Decide(forEach) == Keep.Run)
        {
            return Keep.Run;
        }

        if (!_rows[id].Any(static r => r.Status == NodeRunStatus.Succeeded))
        {
            return Keep.Skipped;
        }

        _branches[id] = [Out];   // the walk decides a taken loop's body and collect with 'out' (GraphWorkflow.Decide)
        return Keep.Ran;
    }

    private Keep Own(NodeId id)
    {
        var mine = _rows[id].ToList();
        if (mine.Count == 0 || !mine.TrueForAll(Done))
        {
            return Keep.Run;
        }

        if (mine.Exists(static r => r.Status == NodeRunStatus.Succeeded))
        {
            return Whole(id) ? Read(id, mine) : Keep.Run;
        }

        return Dead(id) ? Keep.Skipped : Keep.Run;
    }

    // why: a taken step's branch comes from its own row (RerunPlan.Branches); a branching step whose whole row does not say runs again, so the walk never guesses a branch.
    // A row cut at 64 KB is kept with no branch, so the service refuses the re-run (SND-GRAPH-108) instead of running the step again: an ask_owner would ask the owner a second time.
    private Keep Read(NodeId id, List<NodeRun> mine)
    {
        if (RerunPlan.Branches(_nodes[id], mine) is not { } branches)
        {
            return mine.Exists(static r => r.Truncated) ? Keep.Ran : Keep.Run;
        }

        _branches[id] = branches;
        return Keep.Ran;
    }

    // why: a loop is taken whole or runs again whole; a body step with a failed item would otherwise run once as a plain step at item 0 after its for_each was taken
    private bool Whole(NodeId id) => !_loops.TryGetValue(id, out var loop) || loop.PerItem.All(n => _rows[n.Id].All(Done));

    // why: the walk also writes 'skipped' for every step still pending when a failure or a cancel ends the run, so a skipped row is taken only when the taken steps before it left it on a branch nobody took
    private bool Dead(NodeId id)
    {
        var into = Into(id);
        return into.Count > 0 && into.TrueForAll(e => Decide(e.From) == Keep.Skipped || !Took(e.From, e.Output ?? Out));
    }

    private bool Took(NodeId step, string output) => _branches.TryGetValue(step, out var branches) && branches.Contains(output, StringComparer.Ordinal);

    private List<GraphEdge> Into(NodeId id) => [.. _spec.Edges.Where(e => e.To == id)];

    private static bool Done(NodeRun row) => row.Status is NodeRunStatus.Succeeded or NodeRunStatus.Skipped;
}
