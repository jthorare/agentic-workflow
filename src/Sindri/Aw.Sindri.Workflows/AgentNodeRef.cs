using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Which node run an agent-node activity works on (PR 7).</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The node.</param>
/// <param name="ItemIndex">The item's position inside a 'for_each', 0 for a node that runs once.</param>
internal sealed record AgentNodeRef(WorkflowRunId Run, NodeId Node, int ItemIndex);
