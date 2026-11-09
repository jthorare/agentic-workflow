using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>
/// 'workflow complete': a launched agent's report. The recipe engine that took it is gone; the graph engine's agent block (PR 7)
/// brings it back keyed by node runs, so until then the report is refused with SND-GRAPH-104. <see cref="AgentReportAuthority"/>
/// (the launch-secret check) is kept for that block.
/// </summary>
internal sealed class CompleteStepService : IService<CompleteStepRequest, CompleteStepResponse>
{
    public ValueTask<Result<CompleteStepResponse>> Handle(CompleteStepRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<CompleteStepResponse>>(SindriErrors.GraphRunControlsPending.With("reports from a launched agent return with the graph engine's agent block"));
    }
}
