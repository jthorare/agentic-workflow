using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Asks the launcher for a node's session and records the outcome (split from <see cref="AgentNodeLaunching"/>, #2532): the worktree and start commit, or the failure.</summary>
/// <param name="launcher">Starts the session, waiting when the cap is reached.</param>
/// <param name="recorder">Records 'launch.started' and 'launch.failed'.</param>
/// <param name="diff">Reads the start commit.</param>
/// <param name="ended">Answers for a launch whose session already ended, so a retry starts no second agent.</param>
internal sealed class AgentLaunchStart(AgentLaunchWaiting launcher, AgentLaunchRecorder recorder, IRunDiff diff, AgentLaunchEnded ended)
{
    /// <summary>Launches the session.</summary>
    /// <param name="request">The launch.</param>
    /// <param name="cancellationToken">Cancels the launch.</param>
    /// <returns>The worktree and start commit, or the failure, or that the session cap made it wait.</returns>
    public async Task<AgentLaunched> Start(LaunchRequest request, CancellationToken cancellationToken)
    {
        if (await ended.Find(request, cancellationToken).ConfigureAwait(false) is { } over)
        {
            return over;
        }

        return await Launch(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Launches the session with the workflow's resources, made only once the session is known not to have ended (#2586): a retry after the end never fails on them.</summary>
    /// <param name="request">The launch, without the resources.</param>
    /// <param name="resources">Makes the workflow's declared resources exist; idempotent.</param>
    /// <param name="cancellationToken">Cancels the launch.</param>
    /// <returns>The worktree and start commit, or the failure, or that the session cap made it wait.</returns>
    public async Task<AgentLaunched> Start(LaunchRequest request, Func<CancellationToken, ValueTask<Result<ProvisionedResources>>> resources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);
        if (await ended.Find(request, cancellationToken).ConfigureAwait(false) is { } over)
        {
            return over;
        }

        var ensured = await resources(cancellationToken).ConfigureAwait(false);
        return ensured.IsError
            ? new AgentLaunched(Worktree: null, StartHead: null, new AgentLaunchFailure(AgentLaunchFailureKind.StepFailed, ensured.Error.Code, ensured.Error.Message))
            : await Launch(RunAgentLaunches.WithResources(request, ensured.Value), cancellationToken).ConfigureAwait(false);
    }

    private async Task<AgentLaunched> Launch(LaunchRequest request, CancellationToken cancellationToken)
    {
        var result = await launcher.Launch(request, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return new AgentLaunched(Worktree: null, StartHead: null, Failure: null, Waiting: true);   // the cap is still reached: nothing is recorded, the workflow asks again
        }

        switch (result)
        {
            case StartedLaunch started:
                await recorder.Started(request, started, cancellationToken).ConfigureAwait(false);
                var head = await diff.Head(request.Worktree!, cancellationToken).ConfigureAwait(false);   // why: RequestFor always names the worktree
                return new AgentLaunched(request.Worktree, head.IsError ? null : head.Value, Failure: null, Agent: request.Agent.Value, RunDirectory: started.RunDirectory);   // a worktree with no HEAD leaves the diff signal unmeasured
            case FailedLaunch failed:
                await recorder.Failed(request, failed, cancellationToken).ConfigureAwait(false);
                return new AgentLaunched(Worktree: null, StartHead: null, AgentLaunchFailures.Of(failed));
            default:
                throw new InvalidOperationException($"the launcher answered {result.GetType().Name}");
        }
    }
}
