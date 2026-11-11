using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.GetWorkflowRun;

/// <summary>What the run view needs to fill the 'agent' member of each node row.</summary>
/// <param name="Run">The run.</param>
/// <param name="Agents">The ids of the run's 'run_agent' nodes.</param>
/// <param name="Files">The per-launch files: the held marker, used when the run could not be asked (null: none to read).</param>
/// <param name="Asked">True when the run's workflow answered the query.</param>
/// <param name="Waiting">The node the workflow waits on (null: none), when it was asked.</param>
internal sealed record AgentRows(WorkflowRunId Run, HashSet<NodeId> Agents, ILaunchFiles? Files, bool Asked, RunningAgentNode? Waiting)
{
    /// <summary>A run with no agent candidates.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The empty set.</returns>
    internal static AgentRows None(WorkflowRunId run) => new(run, [], Files: null, Asked: false, Waiting: null);

    /// <summary>'waiting' while a run_agent node runs, 'taken_over' while a person holds it; null for any other row.</summary>
    /// <param name="row">The node row.</param>
    /// <returns>The state.</returns>
    internal string? Of(NodeRun row)
    {
        if (row.Status != NodeRunStatus.Running || row.ItemIndex != 0 || !Agents.Contains(row.NodeId))
        {
            return null;
        }

        return Asked ? FromWorkflow(row.NodeId) : FromMarker(row.NodeId);
    }

    private string? FromWorkflow(NodeId node) =>
        Waiting is { ItemIndex: 0 } now && now.Node == node ? (now.TakenOver ? "taken_over" : "waiting") : null;

    private string FromMarker(NodeId node) => Files?.TakenOver(NodeRunAddress.Of(Run, node).Launch()) == true ? "taken_over" : "waiting";
}
