using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.AgentNodeControl;

/// <summary>A person hands the node back (POST /sindri/workflow-runs/{run}/nodes/{node}/handback): its timeout resumes with what was left.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The node.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
internal sealed record HandBackAgentNodeRequest(WorkflowRunId Run, NodeId Node, int ItemIndex) : ICommand<AgentNodeControlled>;
