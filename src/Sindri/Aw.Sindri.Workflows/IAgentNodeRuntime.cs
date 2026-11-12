using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the agent-node activities do (PR 7). The workflow's loop (the wait, the timeout, the takeover) is tested over a fake of this; the real one launches, watches and closes through the launcher.</summary>
internal interface IAgentNodeRuntime
{
    /// <summary>Starts the node run's session: reads the node, resolves the repo, launches in a fresh worktree on the node's branch. Idempotent per launch id.</summary>
    /// <param name="node">The node run.</param>
    /// <param name="cancellationToken">Cancels the launch.</param>
    /// <returns>The outcome.</returns>
    public Task<AgentNodeLaunched> Launch(AgentNodeRef node, CancellationToken cancellationToken);

    /// <summary>Waits for the session to end, calling <paramref name="heartbeat"/> while it does.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="heartbeat">Tells Temporal the wait is alive.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>How it ended.</returns>
    public Task<AgentSessionState> Watch(LaunchId launch, Action heartbeat, CancellationToken cancellationToken);

    /// <summary>Reads the output folder of a session that ended with no report.</summary>
    /// <param name="recover">The node run and its folder.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The report it proves, why it proves none, or nothing.</returns>
    public Task<AgentNodeRecovered> Recover(AgentNodeRecover recover, CancellationToken cancellationToken);

    /// <summary>Reads the usage and the diff signal, then stops the session.</summary>
    /// <param name="run">The launch and what to measure.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The measures.</returns>
    public Task<AgentNodeClosed> Close(AgentCloseRun run, CancellationToken cancellationToken);

    /// <summary>Marks the launch held or free for the idle sweep.</summary>
    /// <param name="held">The launch and whether a person holds it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Done.</returns>
    public Task Hold(AgentNodeHeld held, CancellationToken cancellationToken);

    /// <summary>
    /// Long poll on whether the launch's session waits on a permission prompt (#3151): its fleet state file says asking for an approval. Returns as soon as the ask differs from the one the workflow saw,
    /// or after the cap with nothing new.
    /// </summary>
    /// <param name="watch">The launch, the ask the workflow saw, the read interval and the cap.</param>
    /// <param name="heartbeat">Tells Temporal the call is alive (and delivers a cancel).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tool it asks to use and when; null when the session is not asking for an approval or its state is unknown.</returns>
    public Task<NodeSessionAsk?> WatchAsking(AgentNodeAskWatch watch, Action heartbeat, CancellationToken cancellationToken);
}
