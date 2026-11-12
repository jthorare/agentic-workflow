using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The activities of a 'run_agent' node (PR 7). 'LaunchAgentNode' starts the session (short, idempotent per launch id, so a replay reuses it); 'WatchAgentNode' is long and heartbeats, so a cancel reaches
/// it and a worker restart attaches again to the same session; 'RecoverAgentNode' reads the output folder of a session that ended with no report; 'CloseAgentNode' reads the usage and the diff signal
/// and stops the session; 'HoldAgentNode' tells the idle sweep a person holds the session.
/// </summary>
/// <param name="runtime">What the activities do.</param>
internal sealed class AgentNodeActivities(IAgentNodeRuntime runtime)
{
    /// <summary>Starts the node run's session.</summary>
    /// <param name="node">The node run.</param>
    /// <returns>Where it runs, or why it did not start.</returns>
    [Activity("LaunchAgentNode")]
    public Task<AgentNodeLaunched> LaunchAsync(AgentNodeRef node) => runtime.Launch(node, ActivityExecutionContext.Current.CancellationToken);

    /// <summary>Waits for the launch's session to end. Idempotent: a retry watches the same session.</summary>
    /// <param name="launch">The launch ('LCH-…').</param>
    /// <returns>How it ended.</returns>
    [Activity("WatchAgentNode")]
    public Task<AgentSessionState> WatchAsync(string launch)
    {
        var context = ActivityExecutionContext.Current;
        return runtime.Watch(LaunchId.From(launch), () => context.Heartbeat(), context.CancellationToken);
    }

    /// <summary>Reads the output folder of a session that ended with no report.</summary>
    /// <param name="recover">The node run and its folder.</param>
    /// <returns>The report it proves, why it proves none, or nothing.</returns>
    [Activity("RecoverAgentNode")]
    public Task<AgentNodeRecovered> RecoverAsync(AgentNodeRecover recover) => runtime.Recover(recover, ActivityExecutionContext.Current.CancellationToken);

    /// <summary>Reads the usage and the diff signal, then stops the session.</summary>
    /// <param name="run">The launch and what to measure.</param>
    /// <returns>The measures.</returns>
    [Activity("CloseAgentNode")]
    public Task<AgentNodeClosed> CloseAsync(AgentCloseRun run) => runtime.Close(run, ActivityExecutionContext.Current.CancellationToken);

    /// <summary>Marks the launch held or free for the idle sweep.</summary>
    /// <param name="held">The launch and whether a person holds it.</param>
    /// <returns>Done.</returns>
    [Activity("HoldAgentNode")]
    public Task HoldAsync(AgentNodeHeld held) => runtime.Hold(held, ActivityExecutionContext.Current.CancellationToken);

    /// <summary>Long poll on whether the launch's session waits on a permission prompt (#3151): returns when the ask differs from the one the workflow saw, or at the cap.</summary>
    /// <param name="watch">The launch, the ask seen, the read interval and the cap.</param>
    /// <returns>The tool and when; null when it does not ask.</returns>
    [Activity("AskingAgentNode")]
    public Task<NodeSessionAsk?> AskingAsync(AgentNodeAskWatch watch)
    {
        var context = ActivityExecutionContext.Current;
        return runtime.WatchAsking(watch, () => context.Heartbeat(), context.CancellationToken);
    }
}
