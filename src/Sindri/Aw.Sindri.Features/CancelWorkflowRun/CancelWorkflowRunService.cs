using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.CancelWorkflowRun;

/// <summary>
/// Cancels a run: an unknown run is SND-GRAPH-101, one that already ended SND-GRAPH-107. A queued run has no Temporal workflow yet, so it is marked cancelled
/// in the store and 'workflow.run.cancelled' is appended (the key the workflow's own end uses, so one run never gets two); nothing else starts. Any other
/// run is cancelled through <see cref="IGraphRunCanceller"/>, and its workflow writes the end itself.
/// </summary>
/// <param name="runs">The runs.</param>
/// <param name="canceller">Stops the run's workflow.</param>
/// <param name="ledger">Appends the event of a queued cancel.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
internal sealed class CancelWorkflowRunService(IWorkflowRunStore runs, IGraphRunCanceller canceller, ILedgerStore ledger, HeldLease held, EventFactory events) : IService<CancelWorkflowRunRequest, CancelWorkflowRunResponse>
{
    public async ValueTask<Result<CancelWorkflowRunResponse>> Handle(CancelWorkflowRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = await runs.Get(request.Run, cancellationToken).ConfigureAwait(false);
        if (run.IsError)
        {
            return run.Error;
        }

        if (run.Value.Status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled)
        {
            return await AlreadyEnded(request, run.Value, cancellationToken).ConfigureAwait(false);
        }

        if (run.Value.Status == WorkflowRunStatus.Queued)
        {
            var queued = await CancelQueued(request, cancellationToken).ConfigureAwait(false);
            if (queued.IsError || queued.Value)
            {
                return queued.IsError ? queued.Error : new CancelWorkflowRunResponse(request.Run);
            }

            // why: the run was promoted between our read and the cancel, so it is running in Temporal now and is cancelled through its workflow
        }

        var cancelled = await canceller.Cancel(request.Run, cancellationToken).ConfigureAwait(false);
        return cancelled.IsError ? cancelled.Error : new CancelWorkflowRunResponse(request.Run);
    }

    // why: a queued cancel whose event append failed leaves the row cancelled with this reason and no event; the retry appends it (keyed, so once) and answers success
    private async ValueTask<Result<CancelWorkflowRunResponse>> AlreadyEnded(CancelWorkflowRunRequest request, WorkflowRun run, CancellationToken cancellationToken)
    {
        if (run.Status != WorkflowRunStatus.Cancelled || !string.Equals(run.Error, QueuedReason, StringComparison.Ordinal))
        {
            return SindriErrors.GraphRunEnded.With($"the run '{request.Run.Value}' already ended; there is nothing to cancel");
        }

        var again = await CancelQueued(request, cancellationToken).ConfigureAwait(false);
        return again.IsError ? again.Error : new CancelWorkflowRunResponse(request.Run);
    }

    private const string QueuedReason = "cancelled while queued";

    /// <summary>Cancels a queued run. True: cancelled and the event is in the ledger. False: the run was promoted meanwhile and is not queued any more.</summary>
    private async ValueTask<Result<bool>> CancelQueued(CancelWorkflowRunRequest request, CancellationToken cancellationToken)
    {
        // why: the lease is checked before the store is touched, so a refusal leaves the run queued and a retry works
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        // why: the store cancels only a run that is still queued; a run promoted meanwhile is running in Temporal and must not be marked cancelled behind its back
        var moved = await runs.CancelQueued(request.Run, QueuedReason, cancellationToken).ConfigureAwait(false);
        if (moved.IsError)
        {
            return moved.Error;
        }

        if (moved.Value.Status != WorkflowRunStatus.Cancelled || !string.Equals(moved.Value.Error, QueuedReason, StringComparison.Ordinal))
        {
            return moved.Value.Status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled
                ? SindriErrors.GraphRunEnded.With($"the run '{request.Run.Value}' already ended; there is nothing to cancel")
                : false;
        }

        var entry = events.Entry(EventTypes.WorkflowRunCancelled, new WorkflowRunCancelledPayload(request.Run, QueuedReason, request.By, moved.Value.Mode), $"{EventTypes.WorkflowRunCancelled}:{request.Run.Value}", moved.Value.ItemId?.Value);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : true;
    }
}
