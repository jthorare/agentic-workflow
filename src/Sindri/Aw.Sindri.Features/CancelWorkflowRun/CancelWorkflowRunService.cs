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
            return SindriErrors.GraphRunEnded.With($"the run '{request.Run.Value}' already ended; there is nothing to cancel");
        }

        if (run.Value.Status == WorkflowRunStatus.Queued)
        {
            return await CancelQueued(request, cancellationToken).ConfigureAwait(false);
        }

        var cancelled = await canceller.Cancel(request.Run, cancellationToken).ConfigureAwait(false);
        return cancelled.IsError ? cancelled.Error : new CancelWorkflowRunResponse(request.Run);
    }

    private async ValueTask<Result<CancelWorkflowRunResponse>> CancelQueued(CancelWorkflowRunRequest request, CancellationToken cancellationToken)
    {
        // why: the lease is checked before the store is touched, so a refusal leaves the run queued and a retry works
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var ended = await runs.SetStatus(request.Run, WorkflowRunStatus.Cancelled, outputs: null, error: null, cancellationToken).ConfigureAwait(false);
        if (ended.IsError)
        {
            return ended.Error;
        }

        var entry = events.Entry(EventTypes.WorkflowRunCancelled, new WorkflowRunCancelledPayload(request.Run, "cancelled while queued", request.By), $"{EventTypes.WorkflowRunCancelled}:{request.Run.Value}", ended.Value.ItemId?.Value);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new CancelWorkflowRunResponse(request.Run);
    }
}
