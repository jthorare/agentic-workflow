using System.Text.Json.Nodes;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The activities that read and write a run (issue 2258, PR 6): load its spec, mark it running, record each node run and mirror it into the ledger,
/// and end it (which also starts the next queued run of a 'queue' workflow). Every ledger row is keyed by its fact, so a retried activity records it once.
/// </summary>
/// <param name="runs">Where runs and node runs live.</param>
/// <param name="workflows">Where the revisions live.</param>
/// <param name="queue">Starts the next queued run when one ends.</param>
/// <param name="mirror">Appends the events to the ledger.</param>
/// <param name="itemRepair">Repairs the item links of a run that ended (issue 2588); null does nothing.</param>
internal sealed class GraphRunActivities(IWorkflowRunStore runs, IWorkflowStore workflows, IGraphQueueAdvance queue, GraphMirror mirror, GraphRunItemRepair? itemRepair = null)
{
    /// <summary>Reads the spec of the revision the run walks.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The spec.</returns>
    [Activity("GraphLoad")]
    public async Task<GraphSpec> LoadAsync(WorkflowRunId run)
    {
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var row = GraphFailures.Value(await runs.Get(run, cancel).ConfigureAwait(false));
        return GraphFailures.Value(await workflows.GetRevision(row.WorkflowId, row.Revision, cancel).ConfigureAwait(false)).Spec;
    }

    /// <summary>Marks the run running and mirrors it as started.</summary>
    /// <param name="run">The run.</param>
    /// <returns>Completes when both are written.</returns>
    [Activity("GraphBegin")]
    public async Task BeginAsync(WorkflowRunId run)
    {
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var row = GraphFailures.Value(await runs.SetStatus(run, WorkflowRunStatus.Running, outputs: null, error: null, cancel).ConfigureAwait(false));
        if (row.Status != WorkflowRunStatus.Running)
        {
            return;   // why: the run already ended (the store refuses a move out of a final status), so there is nothing to start and no event to write
        }

        await mirror.AppendStarted(row, cancel).ConfigureAwait(false);
    }

    /// <summary>Writes one node run row and mirrors it as started, finished or failed ('skipped' and 'cancelled' are finished rows with that status).</summary>
    /// <param name="nodeRun">The node run.</param>
    /// <returns>Completes when both are written.</returns>
    [Activity("GraphRecordNode")]
    public async Task RecordNodeAsync(NodeRun nodeRun)
    {
        ArgumentNullException.ThrowIfNull(nodeRun);
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        // why: the node row keeps the block's input and error, which can carry prompt or trigger text; they are scrubbed like the ledger payloads. The output stays as it is: later nodes read it.
        var scrubbed = Scrubbed(nodeRun);
        var stored = GraphFailures.Value(await runs.RecordNodeRun(scrubbed, cancel).ConfigureAwait(false));
        var item = GraphFailures.Value(await runs.Get(nodeRun.RunId, cancel).ConfigureAwait(false)).ItemId?.Value;
        var prefix = $"{stored.RunId.Value}:{stored.NodeId.Value}:{stored.ItemIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        switch (stored.Status)
        {
            case NodeRunStatus.Running:
                await mirror.Append(EventTypes.WorkflowNodeStarted, new WorkflowNodeStartedPayload(stored.RunId, stored.NodeId, stored.ItemIndex, GraphMirror.By), $"{EventTypes.WorkflowNodeStarted}:{prefix}", item, cancel).ConfigureAwait(false);
                break;
            case NodeRunStatus.Failed:
                await mirror.Append(EventTypes.WorkflowNodeFailed, new WorkflowNodeFailedPayload(stored.RunId, stored.NodeId, stored.ItemIndex, mirror.Clean(stored.Error) ?? string.Empty, GraphMirror.By), $"{EventTypes.WorkflowNodeFailed}:{prefix}", item, cancel).ConfigureAwait(false);
                break;
            case NodeRunStatus.Waiting:
            case NodeRunStatus.Succeeded:
            case NodeRunStatus.Skipped:
            case NodeRunStatus.Cancelled:
            default:
                await mirror.Append(EventTypes.WorkflowNodeFinished, new WorkflowNodeFinishedPayload(stored.RunId, stored.NodeId, stored.ItemIndex, GraphMirror.Name(stored.Status), stored.Truncated, GraphMirror.By), $"{EventTypes.WorkflowNodeFinished}:{prefix}", item, cancel).ConfigureAwait(false);
                break;
        }
    }

    // why: the scrub can make a text longer, so Fit runs after it; a cut here sets 'Truncated' like the block's own Fit does (#3486)
    private NodeRun Scrubbed(NodeRun nodeRun)
    {
        var input = StoredJson.Fit(StoredJson.Quote(mirror.Clean(nodeRun.Input)));
        return nodeRun with { Input = input.Text, Truncated = nodeRun.Truncated || input.Cut, Error = mirror.Clean(nodeRun.Error) };
    }

    /// <summary>Mirrors one change of a 'run_agent' node's session into the ledger (#3151). Keyed by the node run and the change's place in its series, so a retry or a replay writes it once.</summary>
    /// <param name="record">The event and its place.</param>
    /// <returns>Completes when the event is written.</returns>
    [Activity("GraphRecordNodeAgent")]
    public async Task RecordNodeAgentAsync(GraphNodeAgent record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var p = record.Payload;
        var item = GraphFailures.Value(await runs.Get(p.RunId, cancel).ConfigureAwait(false)).ItemId?.Value;
        var key = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{EventTypes.WorkflowNodeAgentChanged}:{p.RunId.Value}:{p.NodeId.Value}:{p.ItemIndex}:{record.Sequence}");
        await mirror.Append(EventTypes.WorkflowNodeAgentChanged, p with { WaitingOn = mirror.Clean(p.WaitingOn), LastTool = mirror.Clean(p.LastTool) }, key, item, cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the 'collect' node's own row once a loop is done: 'items' (each item's value in item order; an item whose branch skipped the collect adds nothing) and 'count'.
    /// The values are read here and written here, so they never pass through the workflow's history.
    /// </summary>
    /// <param name="request">The loop's collect node.</param>
    /// <returns>Why the values can't be gathered (a value was cut at 64 KB), or null when the row is written.</returns>
    [Activity("GraphCollect")]
    public async Task<string?> CollectAsync(GraphCollect request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var rows = GraphFailures.Value(await runs.NodeRuns(request.Run, cancel).ConfigureAwait(false))
            .Where(r => r.NodeId == request.Collect && r.ItemIndex > 0 && r.Status == NodeRunStatus.Succeeded)
            .OrderBy(static r => r.ItemIndex)
            .ToList();
        var (output, failure) = Gather(rows);
        if (failure is not null)
        {
            return failure;
        }

        await RecordNodeAsync(new NodeRun(request.Run, request.Collect, 0, NodeRunStatus.Succeeded, Input: null, output, Truncated: false, Error: null, Usage: null)).ConfigureAwait(false);
        return null;
    }

    /// <summary>Builds the 'collect' node's output from the item rows, or says why it can't be: a value was cut at 64 KB, or all the values together do not fit in the 64 KB the store keeps.</summary>
    /// <param name="rows">The collect rows of items 1 to N that succeeded, in item order.</param>
    /// <returns>The output text, or the failure.</returns>
    internal static (string? Output, string? Failure) Gather(IReadOnlyList<NodeRun> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var cut = rows.FirstOrDefault(static r => r.Truncated);
        if (cut is not null)
        {
            return (null, $"the value of item {cut.ItemIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)} was cut at 64 KB; collect a smaller field");
        }

        var items = new JsonArray([.. rows.Select(static r => (StoredJson.Parse(r.Output) as JsonObject)?["value"]?.DeepClone())]);
        var text = new JsonObject { ["items"] = items, ["count"] = rows.Count }.ToJsonString();
        var bytes = System.Text.Encoding.UTF8.GetByteCount(text);
        return bytes > NodeRunLimits.PayloadBytes
            ? (null, $"the collected values from {rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} items are {bytes.ToString(System.Globalization.CultureInfo.InvariantCulture)} bytes, over the {NodeRunLimits.PayloadBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)}-byte (64 KB) cap on a node's output; collect a smaller field or fewer items")
            : (text, null);
    }

    /// <summary>Gives the run its final status, mirrors it, then starts the oldest queued run of the same workflow.</summary>
    /// <param name="finish">How the run ended.</param>
    /// <returns>Completes when the status is written, mirrored, and the next queued run is started.</returns>
    [Activity("GraphFinish")]
    public async Task FinishAsync(GraphFinish finish)
    {
        ArgumentNullException.ThrowIfNull(finish);
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var outputs = finish.Status == WorkflowRunStatus.Succeeded ? await Outputs(finish.Run, cancel).ConfigureAwait(false) : null;
        var ended = GraphFailures.Value(await runs.SetStatus(finish.Run, finish.Status, outputs, finish.Error, cancel).ConfigureAwait(false));
        if (ended.Status == finish.Status)
        {
            // why: the store refuses to move a run that already ended, so a different status here means another writer ended it first and its event stands; the same status is this
            // activity's own retry, and the keyed events are recorded once. The start is appended too (same key as the begin step) for a run cancelled before it began.
            await mirror.AppendStarted(ended, cancel).ConfigureAwait(false);
            if (finish.Status == WorkflowRunStatus.Cancelled)
            {
                await mirror.Append(EventTypes.WorkflowRunCancelled, new WorkflowRunCancelledPayload(finish.Run, mirror.Clean(finish.Error), GraphMirror.By, WorkflowRunMode.Live), $"{EventTypes.WorkflowRunCancelled}:{finish.Run.Value}", ended.ItemId?.Value, cancel).ConfigureAwait(false);
            }
            else
            {
                await mirror.Append(EventTypes.WorkflowRunFinished, new WorkflowRunFinishedPayload(finish.Run, GraphMirror.Name(finish.Status), mirror.Clean(finish.Error), GraphMirror.By, WorkflowRunMode.Live), $"{EventTypes.WorkflowRunFinished}:{finish.Run.Value}", ended.ItemId?.Value, cancel).ConfigureAwait(false);
            }
        }

        // why: best effort and never an error here (issue 2588): a repair that fails must not retry the finish
        if (itemRepair is not null)
        {
            await itemRepair.AfterFinish(finish.Run, cancel).ConfigureAwait(false);
        }

        var next = await queue.StartNextQueued(ended.WorkflowId, cancel).ConfigureAwait(false);
        if (next.IsError && GraphRunStarter.Unreachable(next.Error))
        {
            // why: any other error would repeat for ever and keep this run open (issue 2999), so only a Temporal that is not there yet is retried
            throw new ApplicationFailureException(next.Error.Message, errorType: next.Error.Code);   // why: retried while Temporal is unreachable, so a queued run is not left waiting; the starter put the promoted run back to queued, and the retry's status write and events are no-ops (the end is already recorded)
        }
    }

    /// <summary>
    /// A succeeded run's outputs as JSON: an object keyed by node id holding what each succeeded node gave. A node that ran once gives its outputs object; a node that ran per item
    /// (item index above 0) gives an array of them in item order. Null when nothing succeeded or the text would pass <see cref="NodeRunLimits.PayloadBytes"/>.
    /// </summary>
    private async Task<string?> Outputs(WorkflowRunId run, CancellationToken cancel)
    {
        var rows = GraphFailures.Value(await runs.NodeRuns(run, cancel).ConfigureAwait(false));
        var result = new JsonObject();
        foreach (var group in rows.Where(static n => n.Status == NodeRunStatus.Succeeded && n.Output is not null && !n.Truncated).GroupBy(static n => n.NodeId))
        {
            // why: a cut output is no longer JSON, so that node is left out rather than half-written
            var parsed = group.OrderBy(static n => n.ItemIndex).Select(static n => JsonNode.Parse(n.Output!)).ToList();
            result[group.Key.Value] = group.Any(static n => n.ItemIndex > 0) ? new JsonArray([.. parsed]) : parsed[0];
        }

        var text = result.Count == 0 ? null : result.ToJsonString();
        // why: like a node's output, the run's is kept only up to 64 KB; a cut JSON text is worse than none, so a larger one is not stored
        return text is not null && NodeRunLimits.Cap(text, out var truncated) is not null && !truncated ? text : null;
    }
}
