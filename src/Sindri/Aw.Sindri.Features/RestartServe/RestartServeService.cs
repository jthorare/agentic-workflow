using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.StopProcess;

namespace Aw.Sindri.Features.RestartServe;

/// <summary>
/// Restarts serve onto the installed build (#3052). Human callers only (a session is refused SND-AUTH-002: the catalog class alone lets sessions stop serve). Refuses SND-BUILD-002 when the builds
/// don't differ or either is unknown. Schedules the relauncher first (SND-BUILD-003 when it can't be, and nothing is stopped), then runs the stop path with the reason 'restart', and answers with the
/// runs it leaves open. It never refuses over open runs: agent sessions are separate processes and the workflows live in Temporal's file.
/// </summary>
internal sealed class RestartServeService(ICallerContextAccessor callers, IBuildStamps? builds, IOpenRuns openRuns, IServeRelauncher relauncher, StopProcessService stop)
    : IService<RestartServeRequest, RestartServeResponse>
{
    public async ValueTask<Result<RestartServeResponse>> Handle(RestartServeRequest request, CancellationToken cancellationToken)
    {
        if (callers.Current is not { Class: CallerClass.Human })
        {
            return SindriErrors.AuthRefused.With("restart is the owner's: run it from the app or 'sindri restart' in a terminal, not from an agent session");
        }

        var running = builds?.Running();
        var installed = builds?.Installed();
        if (!StaleBuild.Is(running, installed))
        {
            return SindriErrors.BuildNothingNewer.With(running is null || installed is null
                ? "this serve isn't running from an installed build (a dev run), so there is no newer one to start"
                : $"the running build {running} is the installed one");
        }

        var kept = await openRuns.Read(cancellationToken).ConfigureAwait(false);
        var scheduled = relauncher.Schedule();
        if (scheduled.IsError)
        {
            return scheduled.Error;
        }

        var stopped = await stop.Handle(new StopProcessRequest(StopReason.Restart), cancellationToken).ConfigureAwait(false);
        return stopped.IsError ? stopped.Error : new RestartServeResponse(stopped.Value.Pid, running!, installed!, kept, "scheduled");
    }
}
