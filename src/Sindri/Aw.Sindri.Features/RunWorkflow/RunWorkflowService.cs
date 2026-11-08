using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.RunWorkflow;

// why: registered with the starter in the GraphWorkflow piece, as the starter itself is.
#pragma warning disable MA0182
/// <summary>
/// Starts a run of a workflow's active revision by hand: refuses an unknown workflow (SND-GRAPH-008), one with no active revision (SND-GRAPH-104)
/// and a payload that is not a JSON object (SND-GRAPH-105), then asks <see cref="IGraphRunStarter"/>, which starts, queues or skips by the workflow's concurrency.
/// </summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="runs">The runs; the next manual occurrence comes from them.</param>
/// <param name="starter">Starts, queues or skips the run.</param>
internal sealed class RunWorkflowService(IWorkflowStore workflows, IWorkflowRunStore runs, IGraphRunStarter starter) : IService<RunWorkflowRequest, RunWorkflowResponse>
{
    public ValueTask<Result<RunWorkflowResponse>> Handle(RunWorkflowRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = (workflows, runs, starter);
        throw new NotSupportedException();
    }
}
#pragma warning restore MA0182
