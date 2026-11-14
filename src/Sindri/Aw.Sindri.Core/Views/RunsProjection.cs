using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Views;

/// <summary>Folds the six 'workflow.run.*' and 'workflow.node.*' events into the 'workflow-runs' view (issue 2258, PR 6): one row per run. Item states and run links come with the item projection, not here.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class RunsProjection(EventFactory events) : IProjection
{
    private static readonly string[] _types =
    [
        EventTypes.WorkflowRunStarted, EventTypes.WorkflowRunFinished, EventTypes.WorkflowRunCancelled,
        EventTypes.WorkflowNodeStarted, EventTypes.WorkflowNodeFinished, EventTypes.WorkflowNodeFailed,
    ];

    public IReadOnlyList<string> Views => [ViewNames.WorkflowRuns];

    public bool Handles(string eventType) => _types.Contains(eventType, StringComparer.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        var step = Read(recorded);
        if (step.IsError)
        {
            return step.Error;
        }

        var key = step.Value.Run.Value;
        var current = await rows.Get<WorkflowRunRow>(ViewNames.WorkflowRuns, key, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        // why: a replayed event (a rebuild folds the page again) is at or below the row's sequence and changes nothing.
        var next = current.Value is { } existing && existing.Sequence >= recorded.Sequence ? null : step.Value.Fold(current.Value);
        return next is null ? Unit.Value : await rows.Put(ViewNames.WorkflowRuns, key, next, cancellationToken).ConfigureAwait(false);
    }

    // The run the event is about and how the event changes that run's row. A fold returns null when the event changes nothing: a node or end event for a run with no
    // row, an end event for a run that already ended, a second start.
    private Result<Step> Read(LedgerEvent recorded)
    {
        var at = recorded.Envelope.OccurredAt;
        var sequence = recorded.Sequence;
        return recorded.Envelope.Type switch
        {
            EventTypes.WorkflowRunStarted => events.Payload<WorkflowRunStartedPayload>(recorded).Map(p => new Step(p.RunId, row => row is not null ? null
                : new WorkflowRunRow(p.RunId, p.WorkflowId, p.Revision, p.Trigger, Running, Error: null, at, EndedAt: null, LastNodeId: null, ItemOf(recorded), sequence, string.IsNullOrEmpty(p.Mode) ? WorkflowRunMode.Live : p.Mode))),
            EventTypes.WorkflowRunFinished => events.Payload<WorkflowRunFinishedPayload>(recorded).Map(p => new Step(p.RunId, row => End(row, r => r with { Status = p.Status, Error = p.Error, EndedAt = at, Sequence = sequence }))),
            EventTypes.WorkflowRunCancelled => events.Payload<WorkflowRunCancelledPayload>(recorded).Map(p => new Step(p.RunId, row => End(row, r => r with { Status = Cancelled, Error = p.Reason, EndedAt = at, Sequence = sequence }))),
            EventTypes.WorkflowNodeStarted => events.Payload<WorkflowNodeStartedPayload>(recorded).Map(p => new Step(p.RunId, row => OnNode(row, p.NodeId, sequence))),
            EventTypes.WorkflowNodeFinished => events.Payload<WorkflowNodeFinishedPayload>(recorded).Map(p => new Step(p.RunId, row => OnNode(row, p.NodeId, sequence))),
            _ => events.Payload<WorkflowNodeFailedPayload>(recorded).Map(p => new Step(p.RunId, row => OnNode(row, p.NodeId, sequence))),
        };
    }

    // An end event only closes a run that is still running: whichever of finish and cancel was recorded first wins.
    private static WorkflowRunRow? End(WorkflowRunRow? row, Func<WorkflowRunRow, WorkflowRunRow> close) =>
        row is not null && string.Equals(row.Status, Running, StringComparison.Ordinal) ? close(row) : null;

    private static WorkflowRunRow? OnNode(WorkflowRunRow? row, NodeId node, long sequence) => row is null ? null : row with { LastNodeId = node, Sequence = sequence };

    private static ItemId? ItemOf(LedgerEvent recorded) => recorded.Item is { } item && ItemId.TryFrom(item, out var id) ? id : null;

    private const string Running = "running";
    private const string Cancelled = "cancelled";

    private sealed record Step(WorkflowRunId Run, Func<WorkflowRunRow?, WorkflowRunRow?> Fold);
}
