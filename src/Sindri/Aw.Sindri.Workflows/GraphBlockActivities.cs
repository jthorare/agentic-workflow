using System.Text.Json.Nodes;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
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

        var scope = new GraphNodeScope(row, spec, GraphFailures.Value(await runs.NodeRuns(run, cancel).ConfigureAwait(false)));
        var config = GraphConfig.Interpolate(graphNode.Config, scope.Lookup);
        if (Cut(scope, config) is { } cutConfig)
        {
            return cutConfig;   // why: the config read an output the store cut at 64 KB; the block never runs on a partial value
        }

        var context = new BlockContext(run, node, config, (JsonObject)graphNode.Config.DeepClone(), scope.Lookup);
        var result = await block.Execute(context, cancel).ConfigureAwait(false);
        return Cut(scope, config) ?? (result.IsError ? Failed(config, result.Error) : Succeeded(config, result.Value));   // a condition reads its references inside Execute
    }

    private static GraphBlockOutcome? Cut(GraphNodeScope scope, JsonObject config) => scope.Cut is { } cut
        ? Failed(config, SindriErrors.GraphOutputTruncated.With($"the output of '{cut}' was cut at 64 KB; refer to a smaller field"))
        : null;

    private static GraphBlockOutcome Failed(JsonObject? input, Kernel.Error error) =>
        new(NodeRunStatus.Failed, input?.ToJsonString(), Output: null, [], $"{error.Code}: {error.Message}");

    private static GraphBlockOutcome Succeeded(JsonObject input, BlockResult result)
    {
        var output = new JsonObject();
        foreach (var (name, value) in result.Outputs)
        {
            output[name] = value?.DeepClone();
        }

        return new GraphBlockOutcome(NodeRunStatus.Succeeded, input.ToJsonString(), output.ToJsonString(), result.Taken, Error: null);
    }
}
