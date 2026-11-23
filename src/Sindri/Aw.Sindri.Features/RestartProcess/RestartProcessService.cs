using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.RestartProcess;

/// <summary>
/// Restarts Sindri's first half (first-run spec decision 11): the owner's (SND-AUTH-002 for anyone else). It reads the open set and, with anything open,
/// refuses with SND-SETUP-009 naming each run and stops nothing; an installed build without 'build-info' is SND-SETUP-011. With none open it stops serve the way POST /sindri/stop does and answers the old pid;
/// the caller waits for that pid and the port, then starts the installed build. It runs behind the command gate (AddRestartCommand), so no command
/// starts a run or a job between the check and the stop. Serve keeps no restart state.
/// </summary>
/// <param name="callers">The caller.</param>
/// <param name="openRuns">The open-set read.</param>
/// <param name="stop">The stop, as POST /sindri/stop runs it.</param>
/// <param name="builds">The installed build: one without 'build-info' is unfinished, and serve stays up (SND-SETUP-011).</param>
internal sealed class RestartProcessService(ICallerContextAccessor callers, IOpenRuns openRuns, IServeStop stop, IBuildStamps builds) : IService<RestartProcessRequest, RestartProcessResponse>
{
    public async ValueTask<Result<RestartProcessResponse>> Handle(RestartProcessRequest request, CancellationToken cancellationToken)
    {
        if (OwnerCaller.Refusal(callers, "Restarting Sindri") is { } refused)
        {
            return refused;
        }

        if (builds.Installed() is { } stamp && builds.InstalledBuild() is null)
        {
            return SindriErrors.SetupInstallUnfinished.With($"The installed Sindri ({stamp}) isn't finished installing: it has no build-info (fix: scripts/install-cs.sh --link)");
        }

        var open = await openRuns.Read(cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        if (open.Value.Count > 0)
        {
            return RunsOpen(open.Value);
        }

        var stopped = await stop.Stop(cancellationToken).ConfigureAwait(false);
        return stopped.IsError ? stopped.Error : new RestartProcessResponse(stopped.Value.Pid, stopped.Value.Epoch, stopped.Value.Sequence);
    }

    /// <summary>SND-SETUP-009 with each open run's name, then the line that shows each one.</summary>
    /// <param name="open">The open set, one or more.</param>
    /// <returns>The refusal.</returns>
    internal static Error RunsOpen(IReadOnlyList<OpenRun> open)
    {
        var shows = open.Select(static r => string.Equals(r.Kind, "workflow-run", StringComparison.Ordinal) ? $"sindri wf run-show {r.Ref}"
            : string.Equals(r.Kind, "job", StringComparison.Ordinal) ? $"sindri jobs show {r.Ref}" : $"sindri agents show {r.Ref}");
        return SindriErrors.SetupRestartRunsOpen.With(
            $"Sindri restarts when {open.Count} finish: {string.Join(", ", open.Select(static r => r.Name))} (fix: sindri restart --wait; {string.Join("; ", shows)})");
    }
}
