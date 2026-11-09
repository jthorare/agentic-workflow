namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'agent-report' update is given (PR 7): a report that passed the declared-output check.</summary>
/// <param name="Node">The node's id.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
/// <param name="Outputs">The checked values of the declared outputs, as a JSON object.</param>
/// <param name="Human">True for the human's report of a taken-over node; false for the launched session's.</param>
internal sealed record AgentNodeDelivery(string Node, int ItemIndex, string Outputs, bool Human);
