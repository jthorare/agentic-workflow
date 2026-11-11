namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>How a node run's session ended and what it left behind, as the close step measured it.</summary>
/// <param name="Branch">The branch the worktree was made on.</param>
/// <param name="Commit">The worktree's HEAD when the node ended, or null when it could not be read.</param>
/// <param name="AgentSession">The agent's own session id, or null when it never reported one.</param>
/// <param name="Tokens">The tokens the session spent.</param>
/// <param name="EmptyDiff">True when the worktree changed nothing from where it started.</param>
public sealed record AgentNodeEnd(string Branch, string? Commit, string? AgentSession, int Tokens, bool EmptyDiff);
