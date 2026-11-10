using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.AgentNodeControl;

/// <summary>The run, the node and the item's position a takeover or a handback names.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The node.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
internal sealed record AgentNodeAsk(WorkflowRunId Run, NodeId Node, int ItemIndex);
