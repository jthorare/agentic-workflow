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

        var scope = new Scope(row, spec, GraphFailures.Value(await runs.NodeRuns(run, cancel).ConfigureAwait(false)));
        var config = GraphConfig.Interpolate(graphNode.Config, scope.Lookup);
        if (Cut(scope, config) is { } cutConfig)
        {
            return cutConfig;   // why: the config read an output the store cut at 64 KB; the block never runs on a partial value
        }

        var context = new BlockContext(run, node, config, (JsonObject)graphNode.Config.DeepClone(), scope.Lookup);
        var result = await block.Execute(context, cancel).ConfigureAwait(false);   // a condition reads its references inside Execute
        if (Cut(scope, config) is { } cutAfter)
        {
            return cutAfter;
        }

        if (result.IsError)
        {
            return Failed(config, result.Error);
        }

        return Succeeded(config, result.Value);   // why: Commit waits for the run's end (CommitRunAsync): a later node can still fail the run
    }

    /// <summary>
    /// Runs the after-success step of every block whose node succeeded, once the whole run has succeeded ('only_new' records its keys here). A run that failed or was cancelled never gets here,
    /// so what a node passed on is not marked done while a later node lost it. The call can repeat after a retry: the blocks make their commit safe to run twice.
    /// </summary>
    /// <param name="run">The run, already walked to the end with every node succeeded, skipped or continued past.</param>
    /// <returns>Null when every commit worked, or the reason the first one failed.</returns>
    [Activity("GraphCommitBlocks")]
    public async Task<string?> CommitRunAsync(WorkflowRunId run)
    {
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var row = GraphFailures.Value(await runs.Get(run, cancel).ConfigureAwait(false));
        var spec = GraphFailures.Value(await workflows.GetRevision(row.WorkflowId, row.Revision, cancel).ConfigureAwait(false)).Spec;
        var nodeRuns = GraphFailures.Value(await runs.NodeRuns(run, cancel).ConfigureAwait(false));
        var scope = new Scope(row, spec, nodeRuns);
        foreach (var graphNode in spec.Nodes)
        {
            var stored = nodeRuns.FirstOrDefault(n => n.NodeId == graphNode.Id && n.ItemIndex == 0 && n.Status == NodeRunStatus.Succeeded);
            if (stored is not null && blocks.Find(graphNode.Type) is { } block && await CommitNode(run, graphNode, stored, block, scope, cancel).ConfigureAwait(false) is { } failure)
            {
                return failure;
            }
        }

        return null;
    }

    private static async Task<string?> CommitNode(WorkflowRunId run, GraphNode graphNode, NodeRun stored, IGraphBlock block, Scope scope, CancellationToken cancel)
    {
        // why: a cut output is not what the block gave, so committing it would remember the wrong keys; the items stay unseen and come back on the next run (a repeat, never a loss)
        if (stored.Output is null || stored.Truncated)
        {
            return null;
        }

        var config = GraphConfig.Interpolate(graphNode.Config, scope.Lookup);
        if (scope.Cut is not null || JsonNode.Parse(stored.Output) is not JsonObject output)
        {
            return null;   // why: the config read a cut output, so the keys it names are unknown
        }

        var result = new BlockResult(output.ToDictionary(static p => p.Key, static p => p.Value?.DeepClone(), StringComparer.Ordinal), Branches: null);
        var context = new BlockContext(run, graphNode.Id, config, (JsonObject)graphNode.Config.DeepClone(), scope.Lookup);
        var committed = await block.Commit(context, result, cancel).ConfigureAwait(false);
        return committed.IsError ? $"node '{graphNode.Id.Value}' could not record what it passed on: {committed.Error.Code}: {committed.Error.Message}" : null;
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

    /// <summary>What a reference can read in one run: the trigger, the workflow's own facts and the outputs of the nodes that succeeded.</summary>
    private sealed class Scope(WorkflowRun run, GraphSpec spec, IReadOnlyList<NodeRun> nodeRuns)
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
            "workflow" => new JsonObject { ["name"] = spec.Name, ["runId"] = run.Id.Value, ["revision"] = run.Revision, ["id"] = run.WorkflowId.Value },
            _ => Output(name),
        };

        private JsonNode? Output(string node)
        {
            var found = nodeRuns.FirstOrDefault(n => string.Equals(n.NodeId.Value, node, StringComparison.Ordinal) && n.ItemIndex == 0 && n.Status == NodeRunStatus.Succeeded);
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
