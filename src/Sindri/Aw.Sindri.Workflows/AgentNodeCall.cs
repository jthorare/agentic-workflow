namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'agent-takeover' and 'agent-handback' updates are given (PR 7): which node.</summary>
/// <param name="Node">The node's id.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
internal sealed record AgentNodeCall(string Node, int ItemIndex);
