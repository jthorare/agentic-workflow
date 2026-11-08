using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The ids a 'run_agent' node run uses for its activities, so a replay reaches the same session.</summary>
#pragma warning disable MA0182 // why: red stub; the fix commit registers it with the worker
internal static class AgentNodeIds
{
    /// <summary>The launch activity's id: 'agent-launch:{run}:{node}:{index}'.</summary>
    /// <param name="run">The workflow run.</param>
    /// <param name="node">The node.</param>
    /// <param name="index">The item index; 0 outside a 'for_each'.</param>
    /// <returns>The id.</returns>
    public static string LaunchActivity(WorkflowRunId run, NodeId node, int index) => throw new NotSupportedException();
}
