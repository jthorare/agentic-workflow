using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.RestartProcess;

/// <summary>A stub until the restart commit: refuses a caller without the human token (SND-AUTH-002), then stops nothing.</summary>
/// <param name="callers">The caller.</param>
internal sealed class RestartProcessService(ICallerContextAccessor callers) : IService<RestartProcessRequest, RestartProcessResponse>
{
    public ValueTask<Result<RestartProcessResponse>> Handle(RestartProcessRequest request, CancellationToken cancellationToken) =>
        OwnerCaller.Refusal(callers, "Restarting Sindri") is { } refused
            ? ValueTask.FromResult<Result<RestartProcessResponse>>(refused)
            : throw new NotSupportedException("RestartProcess is not built yet (first-run spec decision 11)");
}
