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

        var nodeRuns = GraphFailures.Value(await runs.NodeRuns(run, cancel).ConfigureAwait(false));
        var (loop, loopFailure) = itemIndex > 0 ? Loop(new Scope(row, spec, nodeRuns, loop: null), spec, node, itemIndex) : (null, null);
        if (loopFailure is not null)
        {
            return loopFailure;
        }

        var scope = new Scope(row, spec, nodeRuns, loop);
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
    private static (LoopContext? Loop, GraphBlockOutcome? Failure) Loop(Scope outer, GraphSpec spec, NodeId node, int itemIndex)
    {
        var shape = LoopBody.Enclosing(spec, node) ?? throw new ApplicationFailureException($"node '{node.Value}' is not inside a for_each", nonRetryable: true);
        var config = GraphConfig.Interpolate(shape.ForEach.Config, outer.Lookup);
        if (Cut(outer, config) is { } cut)
        {
            return (null, cut);
        }

        var item = config["items"] is JsonArray items && itemIndex <= items.Count ? items[itemIndex - 1]?.DeepClone() : null;
        return (new LoopContext(shape, itemIndex, item), null);
    }

    private static GraphBlockOutcome? Cut(Scope scope, JsonObject config) => scope.Cut is { } cut
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

    /// <summary>The loop a node execution runs in: its shape, the item's position counted from 1 and the item.</summary>
    private sealed record LoopContext(LoopShape Shape, int ItemIndex, JsonNode? Item);

    /// <summary>What a reference can read in one run: the trigger, the workflow's own facts, the item inside a loop and the outputs of the nodes that succeeded.</summary>
    private sealed class Scope(WorkflowRun run, GraphSpec spec, IReadOnlyList<NodeRun> nodeRuns, LoopContext? loop)
    {
        private readonly JsonObject? _trigger = JsonNode.Parse(run.Trigger) as JsonObject;

        /// <summary>The node whose cut output a reference read, or null.</summary>
        public string? Cut { get; private set; }

        public JsonNode? Lookup(string reference)
        {
            var parts = reference.Split('.');
            var current = Root(parts[0]);
            foreach (var part in parts.Skip(1))
            {
                current = current is JsonObject o && o.TryGetPropertyValue(part, out var next) ? next : null;
            }

            return current?.DeepClone();
        }

        private JsonNode? Root(string name) => name switch
        {
            "trigger" => _trigger,
            "workflow" => new JsonObject { ["name"] = spec.Name, ["runId"] = run.Id.Value, ["revision"] = run.Revision },
            "item" => loop?.Item,
            "itemIndex" => loop is null ? null : JsonValue.Create(loop.ItemIndex),
            _ => Output(name),
        };

        private JsonNode? Output(string node)
        {
            // why: a node of the loop's body has one row per item and is read at this item's; every other node has its one row at index 0
            var index = loop is not null && loop.Shape.Body.Any(b => string.Equals(b.Id.Value, node, StringComparison.Ordinal)) ? loop.ItemIndex : 0;
            var found = nodeRuns.FirstOrDefault(n => string.Equals(n.NodeId.Value, node, StringComparison.Ordinal) && n.ItemIndex == index && n.Status == NodeRunStatus.Succeeded);
            if (found is null)
            {
                return null;
            }

            if (found.Truncated)
            {
                Cut ??= node;
                return null;
            }

            return found.Output is null ? null : JsonNode.Parse(found.Output);
        }
    }
}
