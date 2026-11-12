using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.StopProcess;

namespace Aw.Sindri.Features.RestartServe;

/// <summary>
/// Restarts serve onto the installed build (#3052). Human callers only (a session is refused SND-AUTH-002). Refuses SND-BUILD-002 when the builds don't differ or either is unknown.
/// Schedules the relauncher first (SND-BUILD-003 when it can't be, and nothing is stopped), then runs the stop path with the reason 'restart', and answers with the runs it leaves open.
/// </summary>
internal sealed class RestartServeService(ICallerContextAccessor callers, IBuildStamps? builds, IOpenRuns openRuns, IServeRelauncher relauncher, StopProcessService stop)
    : IService<RestartServeRequest, RestartServeResponse>
{
    public ValueTask<Result<RestartServeResponse>> Handle(RestartServeRequest request, CancellationToken cancellationToken)
    {
        _ = (callers, builds, openRuns, relauncher, stop);   // why: red stub; the real body uses them
        throw new NotSupportedException();
    }
}
