using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Loops;
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

    // A re-run from a step starts with the earlier steps decided: they leave the pending list, and a succeeded one has taken the branches it took in the source run.
    private void Restore(IReadOnlyList<GraphRestoredNode> restored)
    {
        foreach (var step in restored)
        {
            var id = NodeId.From(step.Node);
            _ = _decided.Add(id);
            if (step.Taken.Count > 0)
            {
                _taken[id] = step.Taken;
            }
        }
    }

    private async Task<GraphWorkflowResult> Walk(GraphSpec spec)
    {
        _pending = [.. spec.Nodes.Where(n => !_decided.Contains(n.Id))];
        // why: a ready node on a branch nobody took is recorded 'skipped' before a live one runs, so a dead branch shows skipped while its sibling parks (a wait or ask_owner can hold the walk for hours).
        // The patch keeps a run started on the old walk order replaying it: its history has the live node's activity next.
        var deadFirst = Workflow.Patched("graph-walk-dead-first");
        while (((deadFirst ? _pending.Find(n => Ready(spec, n) && !Live(spec, n)) : null) ?? _pending.Find(n => Ready(spec, n))) is { } node)
        {
            _ = _pending.Remove(node);
            if (!Live(spec, node))
            {
                await Record(Row(node, NodeRunStatus.Skipped), _write).ConfigureAwait(true);
                _ = _decided.Add(node.Id);
                continue;
            }

            var outcome = await Execute(spec, node).ConfigureAwait(true);
            _ = _decided.Add(node.Id);
            if (await AfterFailure(spec, node, outcome).ConfigureAwait(true) is { } failed)
            {
                return failed;
            }

            _taken[node.Id] = outcome.Status == NodeRunStatus.Succeeded ? outcome.Taken : ["out"];
            if (await AfterForEach(spec, node, outcome).ConfigureAwait(true) is { } ended)
            {
                return ended;
            }
        }

        return await End(WorkflowRunStatus.Succeeded, error: null).ConfigureAwait(true);
    }

    /// <summary>After a failed node: 'stop' ends the run, and so does a 'continue' that has no 'out' edge to continue on. Null when the walk goes on.</summary>
    private async Task<GraphWorkflowResult?> AfterFailure(GraphSpec spec, GraphNode node, GraphBlockOutcome outcome)
    {
        if (outcome.Status != NodeRunStatus.Failed)
        {
            return null;
        }

        if (!string.Equals(node.OnFailure, "continue", StringComparison.Ordinal))
        {
            return await End(WorkflowRunStatus.Failed, $"node '{node.Id.Value}' failed: {outcome.Error}").ConfigureAwait(true);
        }

        // why: a failed node takes no named branch, so a condition or ask_owner with 'continue' skipped every node after it and the run ended succeeded (#3003); patched so a run started before replays as it did
        return Workflow.Patched("graph-continue-nowhere-1") && GraphContinueRule.LeavesNowhere(spec, node)
            ? await End(WorkflowRunStatus.Failed, $"node '{node.Id.Value}' failed and has onFailure continue, but it has no 'out' edge to continue on: {outcome.Error}").ConfigureAwait(true)
            : null;
    }

    /// <summary>After a for_each node: a succeeded one runs its loop, any other skips its body and collect. Null when the walk goes on.</summary>
    private async Task<GraphWorkflowResult?> AfterForEach(GraphSpec spec, GraphNode node, GraphBlockOutcome outcome)
    {
        if (!string.Equals(node.Type, LoopBody.ForEachType, StringComparison.Ordinal))
        {
            return null;
        }

        if (outcome.Status == NodeRunStatus.Succeeded)
        {
            return await RunLoop(spec, node, outcome).ConfigureAwait(true);
        }

        if (LoopBody.Find(spec, node.Id) is { IsSuccess: true } loop)
        {
            await SkipLoop(loop.Value).ConfigureAwait(true);
        }

        return null;
    }

    // why: a for_each that failed and said 'continue' ran no item, so its body and collect must not run once as plain nodes with no item; they are 'skipped' and the walk goes on after the collect
    private async Task SkipLoop(LoopShape shape)
    {
        foreach (var node in shape.PerItem)
        {
            _ = _pending.RemoveAll(n => n.Id == node.Id);
            await Record(Row(node, NodeRunStatus.Skipped), _write).ConfigureAwait(true);
            _ = _decided.Add(node.Id);
            _taken[node.Id] = ["out"];
        }
    }

    private bool Ready(GraphSpec spec, GraphNode node) => GraphWalkRules.Ready(spec, node, _decided);

    private bool Live(GraphSpec spec, GraphNode node) => GraphWalkRules.Live(spec, node, _taken);

    private async Task<GraphBlockOutcome> Execute(GraphSpec spec, GraphNode node)
    {
        _current = node;
        await Record(Row(node, NodeRunStatus.Running), _write).ConfigureAwait(true);
        var outcome = await Park(node, await RunBlock(spec, node).ConfigureAwait(true)).ConfigureAwait(true);   // a wait or ask_owner node sleeps here, still running

        // why: once the block has answered, the node's result is decided; clearing _current first and writing detached means a cancel now cannot overwrite 'succeeded' with 'cancelled'
        _current = null;
        await Record(new NodeRun(_run, node.Id, 0, outcome.Status, outcome.Input, outcome.Output, outcome.Truncated, outcome.Error, Usage: null, Raw: outcome.Raw), _detached).ConfigureAwait(true);
        return outcome;
    }

    private async Task<GraphBlockOutcome> RunBlock(GraphSpec spec, GraphNode node)
    {
        // why: a loop that can't run (no collect, a nested loop) fails its for_each before any item does
        if (string.Equals(node.Type, LoopBody.ForEachType, StringComparison.Ordinal) && LoopBody.Find(spec, node.Id) is { IsError: true } refused)
        {
            return new GraphBlockOutcome(NodeRunStatus.Failed, Input: null, Output: null, [], $"{refused.Error.Code}: {refused.Error.Message}");
        }

        // why: a loop with a run_agent in its body ran no item before #2470; a run that started on that walk still replays the refusal
        if (string.Equals(node.Type, LoopBody.ForEachType, StringComparison.Ordinal) && LoopBody.Find(spec, node.Id) is { IsSuccess: true } loop && LoopBody.AgentRefusal(loop.Value) is { } agentInLoop && !Workflow.Patched("graph-agent-in-loop"))
        {
            return new GraphBlockOutcome(NodeRunStatus.Failed, Input: null, Output: null, [], $"{LoopBody.AgentInLoopCode}: {agentInLoop}");
        }

        try
        {
            return string.Equals(node.Type, "run_agent", StringComparison.Ordinal)
                ? await Agents.Run(node, 0).ConfigureAwait(true)   // why: a session outlives a plain activity and a person may take it over, so the agent loop runs in the workflow (PR 7)
                : await Workflow.ExecuteActivityAsync((GraphBlockActivities a) => a.RunBlockAsync(_run, node.Id, 0), _block).ConfigureAwait(true);
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
