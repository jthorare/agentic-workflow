using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.StopProcess;

namespace Aw.Sindri.Features.RestartProcess;

/// <summary>A stub until the restart commit: refuses a caller without the human token (SND-AUTH-002), then stops nothing.</summary>
/// <param name="callers">The caller.</param>
internal sealed class RestartProcessService(ICallerContextAccessor callers) : IService<RestartProcessRequest, StopProcessResponse>
{
    public ValueTask<Result<StopProcessResponse>> Handle(RestartProcessRequest request, CancellationToken cancellationToken) =>
        OwnerCaller.Refusal(callers, "Restarting Sindri") is { } refused
            ? ValueTask.FromResult<Result<StopProcessResponse>>(refused)
            : throw new NotSupportedException("RestartProcess is not built yet (first-run spec decision 11)");
}
