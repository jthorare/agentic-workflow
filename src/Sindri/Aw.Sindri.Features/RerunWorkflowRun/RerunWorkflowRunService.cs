using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.RerunWorkflowRun;

/// <summary>Re-running a run from a step. A red stub (Workflows page spec §7.3, P7, #3204): it throws until the implementation lands.</summary>
internal sealed class RerunWorkflowRunService : IService<RerunWorkflowRunRequest, RerunWorkflowRunResponse>
{
    public ValueTask<Result<RerunWorkflowRunResponse>> Handle(RerunWorkflowRunRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Re-run from a step is not built yet");
}
