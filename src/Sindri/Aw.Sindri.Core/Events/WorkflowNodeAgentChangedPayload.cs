using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.node.agent.changed' payload (#3151, Agents page E1): the session a 'run_agent' node launched changed state, waiting-on text or last tool.</summary>
/// <param name="RunId">The run's id.</param>
/// <param name="NodeId">The node.</param>
/// <param name="ItemIndex">The loop item, 0 outside a loop.</param>
/// <param name="State">One of <see cref="Graph.NodeAgentStates"/>.</param>
/// <param name="WaitingOn">What the node waits on, for example 'launch slot (3 ahead)'; null when it waits on nothing.</param>
/// <param name="LastTool">The last tool the session used; null when unknown.</param>
/// <param name="LastToolAt">When it used it.</param>
/// <param name="LaunchId">The launch, once there is one.</param>
/// <param name="Branch">The branch the session may push.</param>
/// <param name="Deadline">When the node's timeout ends it; null before launch.</param>
/// <param name="By">Who recorded it.</param>
public sealed record WorkflowNodeAgentChangedPayload(WorkflowRunId RunId, NodeId NodeId, int ItemIndex, string State, string? WaitingOn, string? LastTool, DateTimeOffset? LastToolAt, LaunchId? LaunchId, string? Branch, DateTimeOffset? Deadline, string By);
