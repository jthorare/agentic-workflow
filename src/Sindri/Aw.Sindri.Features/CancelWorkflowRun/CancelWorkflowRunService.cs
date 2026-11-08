using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.CancelWorkflowRun;

// why: registered with the canceller in the GraphWorkflow piece.
#pragma warning disable MA0182
/// <summary>Cancels a run: an unknown run is SND-GRAPH-101, one that already ended SND-GRAPH-107, otherwise <see cref="IGraphRunCanceller"/> is asked.</summary>
/// <param name="runs">The runs.</param>
/// <param name="canceller">Stops the run's workflow.</param>
internal sealed class CancelWorkflowRunService(IWorkflowRunStore runs, IGraphRunCanceller canceller) : IService<CancelWorkflowRunRequest, CancelWorkflowRunResponse>
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

        var cancelled = await canceller.Cancel(request.Run, cancellationToken).ConfigureAwait(false);
        return cancelled.IsError ? cancelled.Error : new CancelWorkflowRunResponse(request.Run);
    }
}
#pragma warning restore MA0182
