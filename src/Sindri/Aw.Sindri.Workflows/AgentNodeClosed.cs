namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'CloseAgentNode' returns (PR 7): the usage and diff signal, plus the commit and the agent's own session id for the node's outputs.</summary>
/// <param name="Tokens">The tokens the session spent.</param>
/// <param name="EmptyDiff">Whether the worktree changed nothing from where it started.</param>
/// <param name="Commit">The worktree's HEAD when the node ended; null when it could not be read.</param>
/// <param name="AgentSession">The agent's own session id; null when it never reported one.</param>
internal sealed record AgentNodeClosed(int Tokens, bool EmptyDiff, string? Commit, string? AgentSession);
