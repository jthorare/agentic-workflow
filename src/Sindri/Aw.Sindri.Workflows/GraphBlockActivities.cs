using System.Text.Json.Nodes;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Loops;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'GraphRunBlock' (issue 2258, PR 6): runs one execution of one node. Resolves the block from the registry, interpolates the node's config with the trigger
/// and the outputs earlier nodes stored, and calls <see cref="IGraphBlock.Execute"/>. A block that fails is an outcome, not an activity failure.
/// </summary>
/// <param name="blocks">The blocks this build has.</param>
/// <param name="runs">Where the earlier nodes' outputs are read from.</param>
/// <param name="workflows">Where the run's spec is read from.</param>
internal sealed class GraphBlockActivities(GraphBlockRegistry blocks, IWorkflowRunStore runs, IWorkflowStore workflows)
{
    /// <summary>Runs one node execution.</summary>
    /// <param name="run">The run.</param>
    /// <param name="node">The node.</param>
    /// <param name="itemIndex">The item's position inside a 'for_each' (PR 9), 0 otherwise.</param>
    /// <returns>What the block gave, or why it failed.</returns>
    [Activity("GraphRunBlock")]
    public async Task<GraphBlockOutcome> RunBlockAsync(WorkflowRunId run, NodeId node, int itemIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemIndex);
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var row = GraphFailures.Value(await runs.Get(run, cancel).ConfigureAwait(false));
        var spec = GraphFailures.Value(await workflows.GetRevision(row.WorkflowId, row.Revision, cancel).ConfigureAwait(false)).Spec;
        var graphNode = spec.Nodes.FirstOrDefault(n => n.Id == node) ?? throw new ApplicationFailureException($"the spec has no node '{node.Value}'", nonRetryable: true);
        if (blocks.Find(graphNode.Type) is not { } block)
        {
            return Failed(input: null, SindriErrors.GraphUnknownBlock.With($"this build has no block '{graphNode.Type}'"));
        }

        // why: a node reads index 0 rows and its own item's rows only, so a loop of N items reads O(N) rows in all, not O(N squared)
        var nodeRuns = GraphFailures.Value(await runs.NodeRunsForItem(run, itemIndex, cancel).ConfigureAwait(false));
        var (loop, loopFailure) = itemIndex > 0 ? Loop(new GraphNodeScope(row, spec, nodeRuns, loop: null), spec, node, itemIndex) : (null, null);
        if (loopFailure is not null)
        {
            return loopFailure;
        }

        var scope = new GraphNodeScope(row, spec, nodeRuns, loop);
        var config = GraphConfig.Interpolate(graphNode.Config, scope.Lookup);
        if (Cut(scope, config) is { } cutConfig)
        {
            return cutConfig;   // why: the config read an output the store cut at 64 KB; the block never runs on a partial value
        }

        var context = new BlockContext(run, node, config, (JsonObject)graphNode.Config.DeepClone(), scope.Lookup);
        var result = await block.Execute(context, cancel).ConfigureAwait(false);
        return Cut(scope, config) ?? (result.IsError ? Failed(config, result.Error) : Succeeded(config, result.Value));   // a condition reads its references inside Execute
    }

    // why: inside a 'for_each' a reference reads the item and the loop's nodes of the same item; the list is read the way the 'for_each' read it, from outputs that never change
    private static (GraphLoopContext? Loop, GraphBlockOutcome? Failure) Loop(GraphNodeScope outer, GraphSpec spec, NodeId node, int itemIndex)
    {
        var shape = LoopBody.Enclosing(spec, node) ?? throw new ApplicationFailureException($"node '{node.Value}' is not inside a for_each", nonRetryable: true);
        var config = GraphConfig.Interpolate(shape.ForEach.Config, outer.Lookup);
        if (Cut(outer, config) is { } cut)
        {
            return (null, cut);
        }

        var item = config["items"] is JsonArray items && itemIndex <= items.Count ? items[itemIndex - 1]?.DeepClone() : null;
        return (new GraphLoopContext(shape, itemIndex, item), null);
    }

    private static GraphBlockOutcome? Cut(GraphNodeScope scope, JsonObject config) => scope.Cut is { } cut
        ? Failed(config, SindriErrors.GraphOutputTruncated.With($"the output of '{cut}' was cut at 64 KB; refer to a smaller field"))
        : null;

    private static GraphBlockOutcome Failed(JsonObject? input, Kernel.Error error) =>
        new(NodeRunStatus.Failed, Fit(input?.ToJsonString()), Output: null, [], NodeRunLimits.Cap($"{error.Code}: {error.Message}", out _));

    private static GraphBlockOutcome Succeeded(JsonObject input, BlockResult result)
    {
        var output = new JsonObject();
        foreach (var (name, value) in result.Outputs)
        {
            output[name] = value?.DeepClone();
        }

        return new GraphBlockOutcome(NodeRunStatus.Succeeded, Fit(input.ToJsonString()), Fit(output.ToJsonString()), result.Taken, Error: null);
    }

    /// <summary>
    /// Cuts a block's input or output before it enters the workflow's history: an activity result over Temporal's 2 MB payload limit fails the workflow task, and Temporal retries it forever.
    /// A cut text keeps one character past the store's 64 KB cap, so the store still cuts it and sets 'Truncated' (a later node that reads it fails with SND-GRAPH-108 instead of reading a half value).
    /// </summary>
    /// <param name="text">The input or output text, or null.</param>
    /// <returns>The text when it fits, otherwise its first 64 KB and an ellipsis.</returns>
    internal static string? Fit(string? text)
    {
        var cut = NodeRunLimits.Cap(text, out var truncated);
        return truncated ? cut + "\u2026" : text;
    }
}
