using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.AgentNodeControl;

/// <summary>A person takes over the 'run_agent' node a run is waiting on (POST /sindri/workflow-runs/{run}/nodes/{node}/takeover): its timeout pauses and only the human reports it.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The node.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
internal sealed record TakeOverAgentNodeRequest(WorkflowRunId Run, NodeId Node, int ItemIndex) : ICommand<AgentNodeControlled>;
