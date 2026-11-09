using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Loops;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// A batch of a 'for_each' (PR 9): for each of its items, in order, walks the loop's nodes (the body, then the 'collect') the way the run's walk does, one activity per
/// block execution, writing a node row per item (the item's position counted from 1 is the row's item index). The batch has its own history, and its size is fixed by the batch size.
/// The first node that fails and says 'stop' ends the batch; the nodes left for that item are 'skipped'.
/// </summary>
[Workflow("GraphIteration")]
internal sealed class GraphIterationWorkflow
{
    private static readonly ActivityOptions _write = GraphActivityOptions.Write;
    private static readonly ActivityOptions _detached = GraphActivityOptions.Detached;
    private static readonly ActivityOptions _block = GraphActivityOptions.Block;

    private WorkflowRunId _run;
    private GraphNode? _current;
    private int _item;

    /// <summary>Runs the batch.</summary>
    /// <param name="input">The batch.</param>
    /// <returns>How it ended: the first failure, or none.</returns>
    [WorkflowRun]
    public async Task<GraphIterationResult> RunAsync(GraphIterationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        _run = input.Run;
        try
        {
            var spec = await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.LoadAsync(_run), _write).ConfigureAwait(true);
            var found = LoopBody.Find(spec, input.ForEach);
            if (found.IsError)
            {
                throw new ApplicationFailureException(found.Error.Message, nonRetryable: true);
            }

            for (var item = input.First; item < input.First + input.Count; item++)
            {
                if (await Walk(spec, found.Value, item).ConfigureAwait(true) is { } failure)
                {
                    return new GraphIterationResult(failure);
                }
            }

            return new GraphIterationResult(Failure: null);
        }
        catch (Exception e) when (TemporalException.IsCanceledException(e))
        {
            if (_current is { } running)
            {
                await Record(Row(running, _item, NodeRunStatus.Cancelled)).ConfigureAwait(true);
            }

            throw;
        }
    }

    private async Task<string?> Walk(GraphSpec spec, LoopShape shape, int item)
    {
        var decided = new HashSet<NodeId> { shape.ForEach.Id };
        var taken = new Dictionary<NodeId, IReadOnlyList<string>> { [shape.ForEach.Id] = ["out"] };
        var pending = shape.PerItem.ToList();
        string? failure = null;
        while (failure is null && pending.Find(n => GraphWalkRules.Ready(spec, n, decided)) is { } node)
        {
            _ = pending.Remove(node);
            if (!GraphWalkRules.Live(spec, node, taken))
            {
                await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(Row(node, item, NodeRunStatus.Skipped)), _write).ConfigureAwait(true);
                _ = decided.Add(node.Id);
                continue;
            }

            var outcome = await Execute(node, item).ConfigureAwait(true);
            _ = decided.Add(node.Id);
            if (outcome.Status == NodeRunStatus.Failed && !string.Equals(node.OnFailure, "continue", StringComparison.Ordinal))
            {
                failure = $"node '{node.Id.Value}' failed on item {item.ToString(System.Globalization.CultureInfo.InvariantCulture)}: {outcome.Error}";
                continue;
            }

            taken[node.Id] = outcome.Status == NodeRunStatus.Succeeded ? outcome.Taken : ["out"];
        }

        foreach (var left in pending)
        {
            await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(Row(left, item, NodeRunStatus.Skipped)), _detached).ConfigureAwait(true);
        }

        return failure;
    }

    private async Task<GraphBlockOutcome> Execute(GraphNode node, int item)
    {
        _current = node;
        _item = item;
        await Record(Row(node, item, NodeRunStatus.Running)).ConfigureAwait(true);
        var outcome = await RunBlock(node, item).ConfigureAwait(true);

        // why: as in the run's walk, once the block has answered the node's result is decided, so the write is not cancelled with the workflow
        _current = null;
        await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(new NodeRun(_run, node.Id, item, outcome.Status, outcome.Input, outcome.Output, Truncated: false, outcome.Error, Usage: null)), _detached).ConfigureAwait(true);
        return outcome;
    }

    private async Task<GraphBlockOutcome> RunBlock(GraphNode node, int item)
    {
        try
        {
            return await Workflow.ExecuteActivityAsync((GraphBlockActivities a) => a.RunBlockAsync(_run, node.Id, item), _block).ConfigureAwait(true);
        }
        catch (ActivityFailureException e) when (!TemporalException.IsCanceledException(e))
        {
            return new GraphBlockOutcome(NodeRunStatus.Failed, Input: null, Output: null, [], e.InnerException?.Message ?? e.Message);
        }
    }

    private NodeRun Row(GraphNode node, int item, NodeRunStatus status) => new(_run, node.Id, item, status, Input: null, Output: null, Truncated: false, Error: null, Usage: null);

    private static Task Record(NodeRun row) => Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.RecordNodeAsync(row), _write);
}
