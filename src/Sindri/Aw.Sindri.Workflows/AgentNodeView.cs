namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'agent-node' query answers (PR 7): the node the run waits on, as plain text and numbers Temporal can carry.</summary>
/// <param name="Node">The node's id.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
/// <param name="Launch">The launch ('LCH-…').</param>
/// <param name="TakenOver">True while a person holds the node.</param>
internal sealed record AgentNodeView(string Node, int ItemIndex, string Launch, bool TakenOver);
