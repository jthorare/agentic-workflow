using System.Globalization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The ids a 'run_agent' node run uses for its activities, so a replay reaches the same session.</summary>
internal static class AgentNodeIds
{
    /// <summary>The launch activity's id: 'agent-launch:{run}:{node}:{index}'.</summary>
    /// <param name="run">The workflow run.</param>
    /// <param name="node">The node.</param>
    /// <param name="index">The item index; 0 outside a 'for_each'.</param>
    /// <returns>The id.</returns>
    public static string LaunchActivity(WorkflowRunId run, NodeId node, int index) => string.Create(CultureInfo.InvariantCulture, $"agent-launch:{run.Value}:{node.Value}:{index}");
}
