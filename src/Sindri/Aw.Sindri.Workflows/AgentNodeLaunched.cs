namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'LaunchAgentNode' returns (PR 7): the launch's outcome, the node's interpolated config for its row, and how long the node may run.</summary>
/// <param name="Launched">The session is up (its worktree and start commit), or why it is not, or that the session cap made it wait.</param>
/// <param name="Input">The interpolated config as JSON, fitted under the 64 KB cap, or null when it was not read.</param>
/// <param name="Timeout">The node's timeout; taken-over time does not count against it.</param>
/// <param name="AskEvery">How often the ask activity reads the session's state file (#3151); null for the default of 2 s.</param>
/// <param name="AskCap">How long one ask call may run before it hands back (#3151); null for the default of 10 minutes.</param>
/// <param name="InputCut">True when <see cref="Input"/> is the envelope for a config that was over the cap (#3486).</param>
internal sealed record AgentNodeLaunched(AgentLaunched Launched, string? Input, TimeSpan Timeout, TimeSpan? AskEvery = null, TimeSpan? AskCap = null, bool InputCut = false);
