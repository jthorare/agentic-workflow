using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxDown;

/// <summary>
/// Stops the project's containers (`docker compose stop`: it removes no container, volume or image, and Sindri's files stay). Stops whatever containers `docker compose ps` shows live (running, restarting or paused) for the project (also after a start that failed, or with an unreadable state.json) and does nothing
/// when none runs or the fetched files were never laid down. With the compose override or .env gone it stops by the compose project label alone (`docker compose -p {project} stop`) and says so. Refuses a Docker that points at another machine (SND-ONYX-010), and takes the same lock as `onyx up`, so it never stops a stack that is being started.
/// </summary>
internal sealed class OnyxDownService(IOnyxWorkspace workspace, OnyxFacts facts, IContainerRuntime runtime) : IService<OnyxDownRequest, OnyxDownResponse>
{
    public ValueTask<Result<OnyxDownResponse>> Handle(OnyxDownRequest request, CancellationToken cancellationToken)
    {
        var lockAttempt = workspace.TryLock();
        if (lockAttempt.IsError)
        {
            return ValueTask.FromResult<Result<OnyxDownResponse>>(lockAttempt.Error);
        }

        using var held = lockAttempt.Value;
        if (held is null)
        {
            return ValueTask.FromResult<Result<OnyxDownResponse>>(SindriErrors.CommandBusy.With("another `sindri onyx` command is running; wait for it to finish, then run this again"));
        }

        var result = Stop(cancellationToken);
        return ValueTask.FromResult(result.IsError ? ComposeFailures.WithHint(result.Error, "run `sindri onyx down` again") : result);
    }

    private Result<OnyxDownResponse> Stop(CancellationToken cancellationToken)
    {
        var picked = facts.ProjectInUse(workspace);
        if (picked.IsError)
        {
            return picked.Error;
        }

        var (project, missing) = picked.Value;
        List<string> warnings = facts.ForeignProjectWarning(workspace.ReadState()) is { } foreign ? [foreign] : [];
        var note = missing.Count == 0 ? null : $"{string.Join(" and ", missing)} missing; stopping by the project label (`docker stop` on the containers labelled {project.Name})";
        return StopLive(project, note, warnings, cancellationToken);
    }

    // What runs is what docker says this home's project has, whatever state.json recorded: a start that failed halfway leaves containers up too.
    private Result<OnyxDownResponse> StopLive(ComposeProject project, string? note, List<string> warnings, CancellationToken cancellationToken)
    {
        var containers = facts.Containers(project, cancellationToken);
        if (containers.IsError)
        {
            return containers.Error;
        }

        IReadOnlyList<string> running = [.. containers.Value.Where(static c => ComposeContainer.IsLive(c.State)).Select(static c => c.Service).Order(StringComparer.Ordinal)];
        if (running.Count == 0)
        {
            return new OnyxDownResponse(WasRunning: false, [], warnings);
        }

        if (note is not null)
        {
            warnings.Add(note);
        }

        var stopped = runtime.Stop(project, cancellationToken);
        if (stopped.IsError)
        {
            return stopped.Error;
        }

        return project.HasFiles ? new OnyxDownResponse(WasRunning: true, running, warnings) : ByLabelOutcome(project, running, warnings, cancellationToken);
    }

    // `docker stop` by id says only that the command ended: ask again and report what is down, and warn about what still runs (a container that ignored the stop, or one restarted by its policy).
    private Result<OnyxDownResponse> ByLabelOutcome(ComposeProject project, IReadOnlyList<string> running, List<string> warnings, CancellationToken cancellationToken)
    {
        var after = facts.Containers(project, cancellationToken);
        if (after.IsError)
        {
            return after.Error;
        }

        IReadOnlyList<string> still = [.. after.Value.Where(static c => ComposeContainer.IsLive(c.State)).Select(static c => c.Service).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        if (still.Count > 0)
        {
            warnings.Add($"still running after the stop: {string.Join(", ", still)}; run `sindri onyx down` again or stop them with `docker stop`");
        }

        return new OnyxDownResponse(WasRunning: true, [.. running.Except(still, StringComparer.Ordinal)], warnings);
    }
}
