namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflow-runs/{run}/nodes/{node}/takeover and /handback.</summary>
/// <param name="ItemIndex">The item's position, 0 (the default, and a missing body) for a node that runs once.</param>
internal sealed record AgentNodeBody(int ItemIndex = 0);
