using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Loops;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// A 'for_each' (PR 9): its items run in batches, each batch a child workflow of <see cref="GraphIterationWorkflow"/> with its own history, so this workflow holds one
/// entry per batch and never one per item. The batches run one after the other, so the items run in order.
/// </summary>
internal sealed partial class GraphWorkflow
{
    /// <summary>Runs the loop whose 'for_each' just succeeded, then writes the 'collect' node's row.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="forEach">The 'for_each' node.</param>
    /// <param name="opened">What the 'for_each' gave: its item count and batch size.</param>
    /// <returns>The run's end when the loop failed a node that stops the run, otherwise null.</returns>
    private async Task<GraphWorkflowResult?> RunLoop(GraphSpec spec, GraphNode forEach, GraphBlockOutcome opened)
    {
        var shape = LoopBody.Find(spec, forEach.Id).Value;   // RunBlock refused a loop that can't be found before the for_each ran
        var (count, batch) = Sizes(opened);

        // why: the collect is the node that is running while the items are; a cancel marks it 'cancelled', and the body nodes, still pending, 'skipped'
        _ = _pending.RemoveAll(n => n.Id == shape.Collect.Id);
        _current = shape.Collect;
        await Record(Row(shape.Collect, NodeRunStatus.Running), _write).ConfigureAwait(true);
        var failure = await RunBatches(forEach, count, batch).ConfigureAwait(true);
        _current = null;

        Decide(shape);
        failure ??= await Workflow.ExecuteActivityAsync((GraphRunActivities a) => a.CollectAsync(new GraphCollect(_run, shape.Collect.Id)), _write).ConfigureAwait(true);
        if (failure is null)
        {
            return null;
        }

        await Record(new NodeRun(_run, shape.Collect.Id, 0, NodeRunStatus.Failed, Input: null, Output: null, Truncated: false, failure, Usage: null), _detached).ConfigureAwait(true);
        return string.Equals(shape.Collect.OnFailure, "continue", StringComparison.Ordinal)
            ? null
            : await End(WorkflowRunStatus.Failed, $"node '{shape.Collect.Id.Value}' failed: {failure}").ConfigureAwait(true);
    }

    private static (int Count, int Batch) Sizes(GraphBlockOutcome opened)
    {
        var counts = JsonNode.Parse(opened.Output ?? "{}");
        return (counts?["count"]?.GetValue<int>() ?? 0, Math.Max(1, counts?["batchSize"]?.GetValue<int>() ?? 1));
    }

    // why: the loop's nodes ran inside the batches, so the walk treats them as decided; a node after the collect reads the collect's row
    private void Decide(LoopShape shape)
    {
        foreach (var node in shape.PerItem)
        {
            _ = _pending.RemoveAll(n => n.Id == node.Id);
            _ = _decided.Add(node.Id);
            _taken[node.Id] = ["out"];
        }
    }

    private async Task<string?> RunBatches(GraphNode forEach, int count, int batch)
    {
        for (var first = 1; first <= count; first += batch)
        {
            var input = new GraphIterationInput(_run, forEach.Id, first, Math.Min(batch, count - first + 1), _agentLaunchAttempts);
            var options = new ChildWorkflowOptions { Id = $"{_run.Value}/{forEach.Id.Value}/{first.ToString(System.Globalization.CultureInfo.InvariantCulture)}" };
            try
            {
                var result = await Workflow.ExecuteChildWorkflowAsync((GraphIterationWorkflow w) => w.RunAsync(input), options).ConfigureAwait(true);
                if (result.Failure is { } failure)
                {
                    return failure;
                }
            }
            catch (ChildWorkflowFailureException e) when (!TemporalException.IsCanceledException(e))
            {
                return e.InnerException?.Message ?? e.Message;
            }
        }

        return null;
    }
}
