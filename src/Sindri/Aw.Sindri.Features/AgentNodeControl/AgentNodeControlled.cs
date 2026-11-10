using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.AgentNodeControl;

/// <summary>Where the node stands after a takeover or a handback.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The node.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
/// <param name="Agent">'taken_over' after a takeover, 'waiting' after a handback.</param>
internal sealed record AgentNodeControlled(WorkflowRunId Run, NodeId Node, int ItemIndex, string Agent);
