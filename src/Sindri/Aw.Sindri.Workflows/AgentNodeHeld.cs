namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'HoldAgentNode' is given (PR 7): whether a person holds the node's session, so the idle sweep leaves it alone.</summary>
/// <param name="Launch">The launch ('LCH-…').</param>
/// <param name="Held">True from a takeover until the handback.</param>
internal sealed record AgentNodeHeld(string Launch, bool Held);
