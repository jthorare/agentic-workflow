namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'LaunchAgentNode' returns (PR 7): the launch's outcome, the node's interpolated config for its row, and how long the node may run.</summary>
/// <param name="Launched">The session is up (its worktree and start commit), or why it is not, or that the session cap made it wait.</param>
/// <param name="Input">The interpolated config as JSON, or null when it was not read.</param>
/// <param name="Timeout">The node's timeout; taken-over time does not count against it.</param>
internal sealed record AgentNodeLaunched(AgentLaunched Launched, string? Input, TimeSpan Timeout);
