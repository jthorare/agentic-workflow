namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// What a human may do to a service right now (the `actions` of the services list): the one place the run, pause and resume rules live, so
/// the RunService admission, the PauseService and ResumeService slices and the list can't drift. The server refuses by the first two predicates
/// (<see cref="CanRun"/>, <see cref="CanPauseOrResume"/>); <see cref="Allowed"/> adds the state that makes an action pointless or refused with 409.
/// </summary>
public static class ServiceActions
{
    /// <summary>Start a manual run.</summary>
    public const string Run = "run";

    /// <summary>Pause the schedule.</summary>
    public const string Pause = "pause";

    /// <summary>Resume the schedule.</summary>
    public const string Resume = "resume";

    /// <summary>Whether a run by name is admitted at all: the approved profile names the service, or it is a built-in (otherwise SND-SVC-404).</summary>
    /// <param name="namedByProfile">True when the approved profile names the service.</param>
    /// <param name="builtIn">True when the build runs it without a profile entry (<see cref="BuiltInService.Is"/>).</param>
    /// <returns>True when a run is admitted.</returns>
    public static bool CanRun(bool namedByProfile, bool builtIn) => namedByProfile || builtIn;

    /// <summary>Whether pause and resume apply to the service: only the approved profile's own services, never a built-in (otherwise SND-SVC-404).</summary>
    /// <param name="namedByProfile">True when the approved profile names the service.</param>
    /// <returns>True when pause and resume apply.</returns>
    public static bool CanPauseOrResume(bool namedByProfile) => namedByProfile;

    /// <summary>
    /// The actions the server would accept and that change something: run unless a manual run is already running (SND-JOB-409; a pause does not
    /// stop a manual run), pause while the schedule runs, resume while it is paused (the server accepts either again, the ledger keeps the intent).
    /// </summary>
    /// <param name="namedByProfile">True when the approved profile names the service.</param>
    /// <param name="builtIn">True when the service is a built-in.</param>
    /// <param name="paused">True when the service is paused.</param>
    /// <param name="jobRunning">True when a manual run of it is running.</param>
    /// <returns>The allowed actions, in the order run, pause, resume.</returns>
    public static IReadOnlyList<string> Allowed(bool namedByProfile, bool builtIn, bool paused, bool jobRunning)
    {
        List<string> actions = [];
        if (CanRun(namedByProfile, builtIn) && !jobRunning)
        {
            actions.Add(Run);
        }

        if (CanPauseOrResume(namedByProfile))
        {
            actions.Add(paused ? Resume : Pause);
        }

        return actions;
    }
}
