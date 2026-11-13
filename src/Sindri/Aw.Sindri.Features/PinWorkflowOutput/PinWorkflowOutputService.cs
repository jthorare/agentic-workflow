using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.PinWorkflowOutput;

/// <summary>Pinning an output. A P0 contract stub (Workflows page spec §14): it refuses with SND-GRAPH-119 until its phase builds it.</summary>
internal sealed class PinWorkflowOutputService : IService<PinWorkflowOutputRequest, PinWorkflowOutputResponse>
{
    public ValueTask<Result<PinWorkflowOutputResponse>> Handle(PinWorkflowOutputRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult<Result<PinWorkflowOutputResponse>>(SindriErrors.GraphNotBuilt.With("Pinning an output is not built yet; the contract is in place"));
    }
}
