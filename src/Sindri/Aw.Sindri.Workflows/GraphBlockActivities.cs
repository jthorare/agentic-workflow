using System.Text.Json.Nodes;
using Aw.Redaction;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Loops;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Resources;
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
/// <param name="provisioner">Creates the workflow's declared resources before a node runs; null on a build with none.</param>
/// <param name="redactor">Scrubs a block's output, error and raw model text before they are cut and stored; null uses the default rules.</param>
internal sealed class GraphBlockActivities(GraphBlockRegistry blocks, IWorkflowRunStore runs, IWorkflowStore workflows, IResourceProvisioner? provisioner = null, IRedactor? redactor = null)
{
    private readonly GraphResourceEnsurer _resources = new(provisioner, workflows);

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

        if (Continued(scope, config) is { } continuedConfig)
        {
            return continuedConfig;   // why: the config read a node that failed and continued (#3235); the block never runs on a value that is not there
        }

        return await Execute(row, spec, new Prepared(graphNode, block, scope, config), itemIndex, cancel).ConfigureAwait(false);
    }

    private async Task<GraphBlockOutcome> Execute(WorkflowRun row, GraphSpec spec, Prepared node, int itemIndex, CancellationToken cancel)
    {
        var (graphNode, block, scope, config) = (node.Node, node.Block, node.Scope, node.Config);
        // why: idempotent, so it runs before every node and the activity keeps no state across a retry; each call is a workflow read plus one ledger upsert transaction (a 20-node run makes 20), acceptable because a graph declares no disk, so a repeat changes no data and creates no files
        var ensured = await _resources.Ensure(row.WorkflowId, spec, cancel).ConfigureAwait(false);
        if (ensured.IsError)
        {
            return Failed(config, ensured.Error);
        }

        var context = new BlockContext(row.Id, graphNode.Id, config, (JsonObject)graphNode.Config.DeepClone(), scope.Lookup)
        {
            Resources = ensured.Value.Env,
            // why: a loop item's batch is a child workflow of the run, so its question names the workflow that waits and the item (items count from 1; 0 is a node outside a loop)
            Item = itemIndex > 0 ? itemIndex : null,
            BatchWorkflow = itemIndex > 0 ? ActivityExecutionContext.Current.Info.WorkflowId : null,
        };
        var result = await block.Execute(context, cancel).ConfigureAwait(false);   // a condition reads its references inside Execute
        if (Cut(scope, config) is { } cutAfter)
        {
            return cutAfter;
        }

        if (Continued(scope, config) is { } continuedAfter)
        {
            return continuedAfter;   // why: a condition reads its references inside Execute
        }

        if (result.IsError)
        {
            return FailedBlock(config, result.Error, context.FailureOutput, context.Raw);
        }

        return SucceededBlock(config, result.Value);   // why: Commit waits for the run's end (CommitRunAsync): a later node can still fail the run
    }

    // why: the block's own text (an http body, a model answer) can hold a secret; it is scrubbed BEFORE Fit cuts it, so a secret the cut would split is gone first (#3225, #3196)
    private GraphBlockOutcome FailedBlock(JsonObject input, Kernel.Error error, JsonObject? output, string? raw)
    {
        var (fitInput, fitOutput, fitRaw) = (StoredJson.Fit(input.ToJsonString()), StoredJson.Fit(CleanJson(output?.ToJsonString())), StoredJson.Fit(Clean(raw)));
        return new(NodeRunStatus.Failed, fitInput.Text, fitOutput.Text, [], NodeRunLimits.Cap(Clean($"{error.Code}: {error.Message}"), out _), fitRaw.Text, fitInput.Cut || fitOutput.Cut || fitRaw.Cut);
    }

    private GraphBlockOutcome SucceededBlock(JsonObject input, BlockResult result)
    {
        var output = new JsonObject();
        foreach (var (name, value) in result.Outputs)
        {
            output[name] = value?.DeepClone();
        }

        var (fitInput, fitOutput) = (StoredJson.Fit(input.ToJsonString()), StoredJson.Fit(CleanJson(output.ToJsonString())));
        return new GraphBlockOutcome(NodeRunStatus.Succeeded, fitInput.Text, fitOutput.Text, result.Taken, Error: null, Truncated: fitInput.Cut || fitOutput.Cut);
    }

    // why: a stored output is parsed as JSON downstream, so a blocked one becomes a JSON string, never the bare marker (#3376)
    private string? CleanJson(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var scrubbed = (redactor ?? Redactor.Default).Scrub(text);
        return scrubbed.IsBlocked ? StoredJson.Blocked : scrubbed.Text;
    }

    private string? Clean(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var scrubbed = (redactor ?? Redactor.Default).Scrub(text);
        return scrubbed.IsBlocked ? StoredJson.BlockedMarker : scrubbed.Text;
    }

    private sealed record Prepared(GraphNode Node, IGraphBlock Block, GraphNodeScope Scope, JsonObject Config);

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
        var outer = new GraphNodeScope(row, spec, nodeRuns, loop: null);
        foreach (var graphNode in spec.Nodes)
        {
            if (blocks.Find(graphNode.Type) is not { } block)
            {
                continue;
            }

            // why: a node inside a for_each body has one row per item (ItemIndex 1..N); each item's row is committed with that item's scope, as Execute read it
            foreach (var stored in nodeRuns.Where(n => n.NodeId == graphNode.Id && n.Status == NodeRunStatus.Succeeded))
            {
                if (ItemScope(outer, row, spec, nodeRuns, stored) is { } scope && await CommitNode(graphNode, stored, block, scope, cancel).ConfigureAwait(false) is { } failure)
                {
                    return failure;
                }
            }
        }

        return null;
    }

    // why: a loop that can't be read again (its list was cut) leaves that item's keys unseen: they come back on the next run (a repeat, never a loss)
    private static GraphNodeScope? ItemScope(GraphNodeScope outer, WorkflowRun row, GraphSpec spec, IReadOnlyList<NodeRun> nodeRuns, NodeRun stored)
    {
        if (stored.ItemIndex == 0)
        {
            return outer;
        }

        var (loop, failure) = Loop(outer, spec, stored.NodeId, stored.ItemIndex);
        return failure is null && loop is not null ? new GraphNodeScope(row, spec, nodeRuns, loop) : null;
    }

    private static async Task<string?> CommitNode(GraphNode graphNode, NodeRun stored, IGraphBlock block, GraphNodeScope scope, CancellationToken cancel)
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
        var context = new BlockContext(stored.RunId, graphNode.Id, config, (JsonObject)graphNode.Config.DeepClone(), scope.Lookup);
        var committed = await block.Commit(context, result, cancel).ConfigureAwait(false);
        return committed.IsError ? $"node '{graphNode.Id.Value}' could not record what it passed on: {committed.Error.Code}: {committed.Error.Message}" : null;
    }

    // why: inside a 'for_each' a reference reads the item and the loop's nodes of the same item; the list is read the way the 'for_each' read it, from outputs that never change
    private static (GraphLoopContext? Loop, GraphBlockOutcome? Failure) Loop(GraphNodeScope outer, GraphSpec spec, NodeId node, int itemIndex)
    {
        var (loop, cutNode) = GraphLoopScopes.Open(outer, spec, node, itemIndex);
        if (cutNode is null)
        {
            return (loop, null);
        }

        // why: a node run with an item index sits in a loop body, so its enclosing loop exists
        var config = GraphConfig.Interpolate(LoopBody.Enclosing(spec, node)!.ForEach.Config, outer.Lookup);
        return (null, Failed(config, SindriErrors.GraphOutputTruncated.With($"the output of '{cutNode}' was cut at 64 KB; refer to a smaller field")));
    }

    private static GraphBlockOutcome? Continued(GraphNodeScope scope, JsonObject config) => scope.ContinuedFailure is var (node, reference)
        ? Failed(config, SindriErrors.GraphReferenceToContinuedFailure.With($"'{node}' failed and continued, so '{{{{{reference}}}}}' has no value"))
        : null;

    private static GraphBlockOutcome? Cut(GraphNodeScope scope, JsonObject config) =>
        StoredJson.ReadFailure(scope, loopCutNode: null) is { } failure ? Failed(config, failure) : null;

    private static GraphBlockOutcome Failed(JsonObject? input, Kernel.Error error)
    {
        var fitInput = StoredJson.Fit(input?.ToJsonString());
        return new(NodeRunStatus.Failed, fitInput.Text, Output: null, [], NodeRunLimits.Cap($"{error.Code}: {error.Message}", out _), Truncated: fitInput.Cut);
    }

    /// <summary>Cuts a block's input or output before it enters the workflow's history; the text that results always parses as JSON (see <see cref="StoredJson.Fit"/>).</summary>
    /// <param name="text">The input or output text, or null.</param>
    /// <returns>The text when it fits, otherwise a small JSON object holding its first part.</returns>
    internal static string? Fit(string? text) => StoredJson.Fit(text).Text;
}
