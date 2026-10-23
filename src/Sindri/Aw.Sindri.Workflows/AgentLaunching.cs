using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What 'LaunchAgentStep' does (engine P8 D4, D8, D12): resolves the target, asks the launcher for the unattended session of <see cref="AgentLaunches.RequestFor"/>, records the outcome,
/// and reads the worktree's HEAD as the run's start. The launcher is idempotent per launch id, so a retry or a replay starts no second session.
/// </summary>
/// <param name="launcher">The launcher.</param>
/// <param name="targets">Where the Step runs.</param>
/// <param name="recorder">Records 'launch.started' and 'launch.failed'.</param>
/// <param name="diff">Reads the start commit.</param>
/// <param name="catalog">The blocks' descriptors, for the Step's brief.</param>
/// <param name="inputs">What a launch reads beside the target: the pack bundles, the stored facts and the Steps' instructions.</param>
internal sealed class AgentLaunching(ILauncher launcher, AgentTargetResolver targets, AgentLaunchRecorder recorder, IRunDiff diff, BlockCatalog catalog, AgentLaunchInputs inputs)
{
    /// <summary>Launches the run's session.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the launch.</param>
    /// <returns>The worktree and start commit, or the failure.</returns>
    public async Task<AgentLaunched> Launch(AgentLaunchRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var target = await targets.Resolve(run.Item, run.Run.Address, cancellationToken).ConfigureAwait(false);
        if (target.IsError)
        {
            return new AgentLaunched(Worktree: null, StartHead: null, new AgentLaunchFailure(AgentLaunchFailureKind.StepFailed, target.Error.Code, target.Error.Message));   // nothing was asked of the launcher, so there is no outcome to record
        }

        var workspace = await WorkspaceOf(run, cancellationToken).ConfigureAwait(false);
        if (workspace.IsError)
        {
            return new AgentLaunched(Worktree: null, StartHead: null, new AgentLaunchFailure(AgentLaunchFailureKind.StepFailed, workspace.Error.Code, workspace.Error.Message));
        }

        var bundle = await Bundle(run, target.Value, cancellationToken).ConfigureAwait(false);
        if (bundle.IsError)
        {
            return new AgentLaunched(Worktree: null, StartHead: null, new AgentLaunchFailure(string.Equals(bundle.Error.Code, PackErrors.PinMismatch.Code, StringComparison.Ordinal) ? AgentLaunchFailureKind.Park : AgentLaunchFailureKind.StepFailed, bundle.Error.Code, bundle.Error.Message));   // an unpinned or changed pack parks for the owner (SND-PACK-002); any other pack failure ends the Step
        }

        var block = BlockId.From(run.Run.Step.Block);
        var told = PayloadFacts.WithShapes(inputs.Instructions.FirstOrDefault(i => i.IsFor(block))?.Lines, run.Payloads);   // the Step's own lines, then the shape of each payload output
        var request = AgentLaunches.RequestFor(run.Run, target.Value, catalog.Find(block), new AgentLaunchExtras(told, workspace.Value, bundle.Value));
        var result = await launcher.Launch(request, cancellationToken).ConfigureAwait(false);
        switch (result)
        {
            case StartedLaunch started:
                await recorder.Started(request, started, cancellationToken).ConfigureAwait(false);
                var head = await diff.Head(request.Worktree!, cancellationToken).ConfigureAwait(false);   // why: RequestFor always names the worktree
                return new AgentLaunched(request.Worktree, head.IsError ? null : head.Value, Failure: null);   // a worktree with no HEAD leaves the diff signal unmeasured
            case FailedLaunch failed:
                await recorder.Failed(request, failed, cancellationToken).ConfigureAwait(false);
                return new AgentLaunched(Worktree: null, StartHead: null, AgentLaunchFailures.Of(failed));
            default:
                throw new InvalidOperationException($"the launcher answered {result.GetType().Name}");
        }
    }

    // The pack the Step's 'bundle' names, resolved and pin-checked at launch; none (a null bundle) when the Step bundles nothing.
    private async ValueTask<Result<PackBundle?>> Bundle(AgentLaunchRun run, AgentStepTarget target, CancellationToken cancellationToken)
    {
        if (run.Bundle is not { } name)
        {
            return (PackBundle?)null;
        }

        var resolved = await inputs.Bundles.Resolve(new PackBundleRequest(name, target.Repo, run.Subject, await HeadLens(run.LensQueue, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        return resolved.Value;
    }

    // The first lens id of the stored lens queue, a JSON array of ids; null when there is no queue, it can't be read or it is empty.
    private async ValueTask<string?> HeadLens(FactValue? queue, CancellationToken cancellationToken)
    {
        if (queue is null || (await inputs.Contents.Read(queue, cancellationToken).ConfigureAwait(false)) is not { IsError: false } bytes)
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(bytes.Value);
            var first = document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array && document.RootElement.GetArrayLength() > 0 ? document.RootElement[0] : default;
            return first.ValueKind == System.Text.Json.JsonValueKind.String ? first.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>The worktree path the stored 'fact:workspace' holds; null when the Step reads none.</summary>
    private async Task<Result<string?>> WorkspaceOf(AgentLaunchRun run, CancellationToken cancellationToken)
    {
        if (run.Workspace is not { } stored)
        {
            return (string?)null;
        }

        var path = await PhaseWorkspace.PathOf(inputs.Contents, stored, cancellationToken).ConfigureAwait(false);
        return path.IsError ? path.Error : path.Value;
    }
}
