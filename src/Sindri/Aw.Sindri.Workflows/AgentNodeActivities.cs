using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The agent runtime's activities for a graph node run: the node-run twins of 'LaunchAgentStep' and 'WatchAgentStep', which stay for the recipe engine until it is deleted.</summary>
#pragma warning disable MA0182 // why: red stub; the fix commit registers it with the worker
internal static class AgentNodeActivities
{
    /// <summary>Starts the node run's session; idempotent per launch id.</summary>
    /// <param name="run">The node run.</param>
    /// <returns>Where it runs, or why it did not start.</returns>
    [Activity("LaunchAgentNode")]
    public static Task<AgentLaunched> LaunchNodeAsync(AgentNodeRun run) => throw new NotSupportedException();

    /// <summary>Waits for the launch's session to end; a retry watches the same session.</summary>
    /// <param name="launch">The launch ('LCH-…').</param>
    /// <returns>How it ended.</returns>
    [Activity("WatchAgentNode")]
    public static Task<AgentSessionState> WatchNodeAsync(string launch) => throw new NotSupportedException();
}
