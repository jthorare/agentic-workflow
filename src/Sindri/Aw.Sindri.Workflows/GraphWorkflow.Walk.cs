using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The walk: topological order, ties by spec order, a node runs when an edge into it was taken.</summary>
internal sealed partial class GraphWorkflow
{
    private readonly HashSet<NodeId> _decided = [];
    private readonly Dictionary<NodeId, IReadOnlyList<string>> _taken = [];
    private List<GraphNode> _pending = [];
    private GraphNode? _current;

    private async Task<GraphWorkflowResult> Walk(GraphSpec spec)
    {
        _pending = [.. spec.Nodes];
        while (_pending.Find(n => Ready(spec, n)) is { } node)
        {
            _ = _pending.Remove(node);
            if (!Live(spec, node))
            {
                await Record(Row(node, NodeRunStatus.Skipped), _write).ConfigureAwait(true);
                _ = _decided.Add(node.Id);
                continue;
            }

            var outcome = await Execute(node).ConfigureAwait(true);
            _ = _decided.Add(node.Id);
            if (outcome.Status == NodeRunStatus.Failed && !string.Equals(node.OnFailure, "continue", StringComparison.Ordinal))
            {
                return await End(WorkflowRunStatus.Failed, $"node '{node.Id.Value}' failed: {outcome.Error}").ConfigureAwait(true);
            }

            _taken[node.Id] = outcome.Status == NodeRunStatus.Succeeded ? outcome.Taken : ["out"];
        }

        return await End(WorkflowRunStatus.Succeeded, error: null).ConfigureAwait(true);
    }

    // why: every edge into the node leaves a node that is already decided (ran or was skipped)
    private bool Ready(GraphSpec spec, GraphNode node) => spec.Edges.Where(e => e.To == node.Id).All(e => _decided.Contains(e.From));

    // why: a trigger has no edge into it and always runs; any other node runs when some edge into it left by a branch its source took
    private bool Live(GraphSpec spec, GraphNode node)
    {
        var incoming = spec.Edges.Where(e => e.To == node.Id).ToList();
        return incoming.Count == 0 || incoming.Exists(e => _taken.TryGetValue(e.From, out var branches) && branches.Contains(e.Output ?? "out", StringComparer.Ordinal));
    }

    private async Task<GraphBlockOutcome> Execute(GraphNode node)
    {
        _current = node;
        await Record(Row(node, NodeRunStatus.Running), _write).ConfigureAwait(true);
        var outcome = await RunBlock(node).ConfigureAwait(true);

        // why: once the block has answered, the node's result is decided; clearing _current first and writing detached means a cancel now cannot overwrite 'succeeded' with 'cancelled'
        _current = null;
        await Record(new NodeRun(_run, node.Id, 0, outcome.Status, outcome.Input, outcome.Output, Truncated: false, outcome.Error, Usage: null), _detached).ConfigureAwait(true);
        return outcome;
    }

    private async Task<GraphBlockOutcome> RunBlock(GraphNode node)
    {
        try
        {
            return await Workflow.ExecuteActivityAsync((GraphBlockActivities a) => a.RunBlockAsync(_run, node.Id, 0), _block).ConfigureAwait(true);
        }
        catch (ActivityFailureException e) when (!TemporalException.IsCanceledException(e))
        {
            return new GraphBlockOutcome(NodeRunStatus.Failed, Input: null, Output: null, [], e.InnerException?.Message ?? e.Message);
        }
    }

    // why: the row carries no payload (input, output, usage), so Truncated is false here; the store sets it when it cuts a real payload
    private NodeRun Row(GraphNode node, NodeRunStatus status) => new(_run, node.Id, 0, status, Input: null, Output: null, Truncated: false, Error: null, Usage: null);

    private static Task Record(NodeRun row, ActivityOptions options) => Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(row), options);

    private async Task<GraphWorkflowResult> End(WorkflowRunStatus status, string? error)
    {
        // why: the walk has decided how the run ends, so the end is written detached; a cancel that arrives now finds the run already final (the store refuses a second end)
        await SkipRest(_detached).ConfigureAwait(true);
        if (status == WorkflowRunStatus.Succeeded && Workflow.Patched("graph-commit-run-end"))
        {
            // why: a block remembers what it passed on (only_new) only when the whole run worked; a failed commit fails the run, and the items come back on the next one
            if (await Workflow.ExecuteActivityAsync((GraphBlockActivities a) => a.CommitRunAsync(_run), _detached).ConfigureAwait(true) is { } why)
            {
                status = WorkflowRunStatus.Failed;
                error = why;
            }
        }

        await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.FinishAsync(new GraphFinish(_run, status, error)), _detached).ConfigureAwait(true);
        return new GraphWorkflowResult(status);
    }

    // why: nodes that never ran are 'skipped' (a failure halted the walk, or a cancel came)
    private async Task SkipRest(ActivityOptions options)
    {
        foreach (var node in _pending.ToList())
        {
            await Record(Row(node, NodeRunStatus.Skipped), options).ConfigureAwait(true);
            _ = _pending.Remove(node);
        }
    }
}
