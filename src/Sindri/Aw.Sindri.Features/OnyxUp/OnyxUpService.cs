using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>
/// Spec §4.1 steps 1-5: preflight, then fetch, config, pull and start, each recorded in state.json with the stamp of its inputs only after it succeeds, so a rerun, an
/// interruption or a crash resumes at the first step whose inputs changed or whose result is gone and repeats nothing earlier. One `up` runs at a time (the workspace lock).
/// The port is the preflight's (3189, or the first free fallback), except that while our own nginx publishes a port (ps shows it) that port stays.
/// A Docker that is starting or not running yet is waited for first (<see cref="OnyxDockerWait"/>).
/// </summary>
internal sealed class OnyxUpService(IOnyxWorkspace workspace, OnyxFacts facts, IOnyxPinsSource pinsSource, IOnyxProgress progress, OnyxDockerWait dockerWait, IEnumerable<IOnyxUpStep> steps)
    : IService<OnyxUpRequest, OnyxUpResponse>
{
    private readonly IReadOnlyList<IOnyxUpStep> _steps = OnyxUpPlan.Validate(steps);

    public async ValueTask<Result<OnyxUpResponse>> Handle(OnyxUpRequest request, CancellationToken cancellationToken)
    {
        var lockAttempt = workspace.TryLock();
        if (lockAttempt.IsError)
        {
            return lockAttempt.Error;
        }

        using var held = lockAttempt.Value;
        if (held is null)
        {
            return SindriErrors.CommandBusy.With("another `sindri onyx up` is running; wait for it to finish, then run this again");
        }

        Result<OnyxUpResponse> result;
        try
        {
            result = await Run(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A file the steps write (the deployment files, .env, the override, state.json) could not be written: the workspace is unusable, not a crash.
            result = SindriErrors.OnyxWorkspaceUnusable.With($"Sindri couldn't write under {facts.Paths.Root}: {e.Message}; make that directory writable by you, then run this again");
        }

        return result.IsError ? ComposeFailures.WithHint(result.Error, "run `sindri onyx up` again to resume") : result;
    }

    private async ValueTask<Result<OnyxUpResponse>> Run(CancellationToken cancellationToken)
    {
        var state = workspace.ReadState();
        if (state is null && workspace.StateIsUnreadable())
        {
            progress.Line("state.json is unreadable; starting over from the first step (each step checks what is already there)");
        }

        var foreign = facts.ForeignProjectWarning(state);
        if (foreign is not null)
        {
            progress.Line(foreign);
        }

        var pins = pinsSource.Pins;
        await dockerWait.Wait(cancellationToken).ConfigureAwait(false);
        var preflight = Preflight(state, pins, cancellationToken);
        if (preflight.IsError)
        {
            return preflight.Error;
        }

        var (port, platform, warnings) = preflight.Value;
        var context = new OnyxUpContext(pins, facts.Paths, port, platform) { Project = facts.Project };
        var recorded = RecordedStamps(state, foreignState: foreign is not null);
        context.KeyPointer = foreign is null ? state?.KeyPointer : null;
        var results = await RunSteps(context, recorded, cancellationToken).ConfigureAwait(false);
        if (results.IsError)
        {
            return results.Error;
        }

        _ = workspace.WriteIfChanged(facts.Paths.Ensure, "running\n", secret: false);   // a finished `onyx up` answers serve's earlier "failed" note
        return new OnyxUpResponse(port, string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"), context.Version ?? pins.Version, warnings, results.Value);
    }

    // A state another home wrote vouches for nothing here: start from step 1 like an unreadable one (each step still re-verifies what is on disk).
    private static Dictionary<string, string> RecordedStamps(OnyxSetupState? state, bool foreignState) =>
        new(foreignState ? [] : state?.Steps ?? new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);

    private async ValueTask<Result<List<OnyxStepResult>>> RunSteps(OnyxUpContext context, Dictionary<string, string> recorded, CancellationToken cancellationToken)
    {
        List<OnyxStepResult> results = [];
        string? upstream = null;
        foreach (var step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stamp = step.Stamp(context, upstream);
            var outcome = await RunStep(step, stamp, context, recorded, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                return outcome.Error;
            }

            results.Add(new OnyxStepResult(step.Name, outcome.Value));
            upstream = stamp;
        }

        return results;
    }

    // The step makes its result hold; its stamp is recorded once it does.
    private async ValueTask<Result<OnyxStepOutcome>> RunStep(IOnyxUpStep step, string stamp, OnyxUpContext context, Dictionary<string, string> recorded, CancellationToken cancellationToken)
    {
        _ = recorded.TryGetValue(step.Name, out var before);
        var outcome = await step.Execute(context, stamp, before, cancellationToken).ConfigureAwait(false);
        if (outcome.IsError)
        {
            return outcome.Error;
        }

        if (outcome.Value == OnyxStepOutcome.Skipped)
        {
            progress.Line($"{step.Name}: already done");
        }

        if (!string.Equals(before, stamp, StringComparison.Ordinal))
        {
            recorded[step.Name] = stamp;
            workspace.WriteState(new OnyxSetupState(context.Port, new Dictionary<string, string>(recorded, StringComparer.Ordinal), context.Project.Name, KeyPointer: context.KeyPointer));
        }

        return outcome;
    }

    // Step 1. The port row accepts the port our own running nginx holds; a failed row stops everything before anything is fetched or written.
    private Result<(int Port, string Platform, IReadOnlyList<string> Warnings)> Preflight(OnyxSetupState? state, OnyxPins pins, CancellationToken cancellationToken)
    {
        var (docker, host) = facts.Read(state?.Port, cancellationToken);
        var ownPort = OwnPort(cancellationToken);
        if (ownPort.IsError)
        {
            return ownPort.Error;
        }

        var report = OnyxPreflight.Evaluate(docker, host, pins, ownPort.Value, facts.MemoryUsedByOthers(cancellationToken));
        string? demoted = null;
        if (report.Blocks)
        {
            var failed = report.Checks.Where(static c => c.State == CheckState.Fail).ToList();
            if (!AlreadyPulled(state) || failed.Exists(static c => c.Name != "disk"))
            {
                return failed[0].Problem!;   // why: a failed check always carries its error
            }

            // The images are already on this box, so the space they use is no longer free: that is not a reason to stop a rerun, which changes nothing.
            var note = $"disk: {failed[0].Detail} (the images are already pulled, so this isn't blocking)";
            progress.Line(note);
            demoted = note;
        }

        var warnings = report.Checks.Where(static c => c.State == CheckState.Warn).Select(static c => $"{c.Name}: {c.Detail}").ToList();
        warnings.ForEach(progress.Line);
        if (demoted is not null)
        {
            warnings.Add(demoted);   // already shown above; kept here so --json carries it
        }

        var platform = facts.Platform(cancellationToken);
        return platform.IsError ? platform.Error : (report.Port, platform.Value, warnings);
    }

    private static bool AlreadyPulled(OnyxSetupState? state) => state is not null && state.Steps.ContainsKey(OnyxSteps.Pull) && state.Steps.ContainsKey(OnyxSteps.Start);

    // The port our own running nginx publishes, whatever state.json says: a start that failed or timed out leaves it up, and a lost state file doesn't make it someone else's.
    private Result<int?> OwnPort(CancellationToken cancellationToken)
    {
        if (facts.DockerProblem(cancellationToken) is not null)
        {
            return Results.Success<int?>(value: null);   // a remote or unreadable endpoint is refused by the preflight; nothing asks it anything before that
        }

        var picked = facts.ProjectInUse(workspace);   // with any of the three files gone the project is asked about by its label: its containers still hold the port
        if (picked.IsError)
        {
            return picked.Error;
        }

        var containers = facts.Containers(picked.Value.Project, cancellationToken);
        return Results.Success(containers.IsSuccess ? OnyxFacts.OwnPort(containers.Value) : null);
    }
}
