using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
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
/// <param name="instructions">What each Step that has instructions is told to do.</param>
/// <param name="contents">Reads a stored 'fact:workspace' back.</param>
internal sealed class AgentLaunching(
    ILauncher launcher, AgentTargetResolver targets, AgentLaunchRecorder recorder, IRunDiff diff, BlockCatalog catalog,
    IEnumerable<IAgentStepInstructions> instructions, IFactContents contents)
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

        var block = BlockId.From(run.Run.Step.Block);
        var told = instructions.FirstOrDefault(i => i.IsFor(block))?.Lines;
        var request = AgentLaunches.RequestFor(run.Run, target.Value, catalog.Find(block), told, workspace.Value);
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

    /// <summary>The worktree path the stored 'fact:workspace' holds; null when the Step reads none.</summary>
    private async Task<Result<string?>> WorkspaceOf(AgentLaunchRun run, CancellationToken cancellationToken)
    {
        if (run.Workspace is not { } stored)
        {
            return (string?)null;
        }

        var path = await PhaseWorkspace.PathOf(contents, stored, cancellationToken).ConfigureAwait(false);
        return path.IsError ? path.Error : path.Value;
    }
}
