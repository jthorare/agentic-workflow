using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.StopProcess;

namespace Aw.Sindri.Features.RestartProcess;

/// <summary>A stub until the restart commit: no caller check, no stop.</summary>
internal sealed class RestartProcessService : IService<RestartProcessRequest, StopProcessResponse>
{
    public ValueTask<Result<StopProcessResponse>> Handle(RestartProcessRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("RestartProcess is not built yet (first-run spec decision 11)");
}
