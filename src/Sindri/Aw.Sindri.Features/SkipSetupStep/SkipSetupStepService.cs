using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.SkipSetupStep;

/// <summary>A stub until the first-run P0 skip commit: writes nothing.</summary>
internal sealed class SkipSetupStepService : IService<SkipSetupStepRequest, SkipSetupStepResponse>
{
    public ValueTask<Result<SkipSetupStepResponse>> Handle(SkipSetupStepRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("SkipSetupStep is not built yet (first-run spec §3.1)");
}
