namespace Aw.Sindri.Ui.Api.Workflows;

// ASSUMED SHAPE (#2472): the body of the take over and hand back calls.
/// <summary>The body of a take over or hand back call.</summary>
/// <param name="ItemIndex">The item of a for_each node, 0 for a node that runs once.</param>
internal sealed record AgentNodeBody(int ItemIndex);
