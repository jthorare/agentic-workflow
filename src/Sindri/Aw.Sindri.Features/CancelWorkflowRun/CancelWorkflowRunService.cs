using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.CancelWorkflowRun;

// why: registered with the canceller in the GraphWorkflow piece.
#pragma warning disable MA0182
/// <summary>Cancels a run: an unknown run is SND-GRAPH-101, one that already ended SND-GRAPH-106, otherwise <see cref="IGraphRunCanceller"/> is asked.</summary>
/// <param name="runs">The runs.</param>
/// <param name="canceller">Stops the run's workflow.</param>
internal sealed class CancelWorkflowRunService(IWorkflowRunStore runs, IGraphRunCanceller canceller) : IService<CancelWorkflowRunRequest, CancelWorkflowRunResponse>
{
    public ValueTask<Result<CancelWorkflowRunResponse>> Handle(CancelWorkflowRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = (runs, canceller);
        throw new NotSupportedException();
    }
}
#pragma warning restore MA0182
