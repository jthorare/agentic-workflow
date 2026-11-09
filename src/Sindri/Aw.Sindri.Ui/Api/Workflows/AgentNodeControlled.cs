namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>POST /sindri/workflow-runs/{run}/nodes/{node}/takeover and .../handback: what the agent node is now.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The node.</param>
/// <param name="ItemIndex">The item the call was for, 0 for a node that runs once.</param>
/// <param name="Agent">taken_over after a take over, waiting after a hand back.</param>
internal sealed record AgentNodeControlled(string Run, string Node, int ItemIndex, string Agent);
