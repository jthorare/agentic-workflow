namespace Aw.Sindri.Ui.Api.Workflows;

// ASSUMED SHAPE (#2472): the real answer comes with the Api half of the issue; swap this record and its two routes in WorkflowRoutes when it lands.
/// <summary>POST /sindri/workflow-runs/{run}/nodes/{node}/takeover and .../handback: what the agent node is now.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The node.</param>
/// <param name="ItemIndex">The item the call was for, 0 for a node that runs once.</param>
/// <param name="Agent">taken_over after a take over, waiting after a hand back.</param>
internal sealed record AgentNodeControlled(string Run, string Node, int ItemIndex, string Agent);
