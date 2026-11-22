using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.RestartProcess;

/// <summary>A stub until the restart commit: refuses a caller without the human token (SND-AUTH-002), then stops nothing.</summary>
/// <param name="callers">The caller.</param>
/// <param name="openRuns">The open-set read.</param>
/// <param name="stop">The stop, as POST /sindri/stop runs it.</param>
internal sealed class RestartProcessService(ICallerContextAccessor callers, IOpenRuns openRuns, IServeStop stop) : IService<RestartProcessRequest, RestartProcessResponse>
{
    public ValueTask<Result<RestartProcessResponse>> Handle(RestartProcessRequest request, CancellationToken cancellationToken) =>
        OwnerCaller.Refusal(callers, "Restarting Sindri") is { } refused
            ? ValueTask.FromResult<Result<RestartProcessResponse>>(refused)
            : throw new NotSupportedException($"RestartProcess is not built yet (first-run spec decision 11; {openRuns.GetType().Name}, {stop.GetType().Name})");
}
