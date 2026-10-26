namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'RecoverAgentStep' is given (issue #1000): the run directory the ended session wrote to, the Step's block, and the run's token (it keys the recovered report).</summary>
/// <param name="Worktree">The run directory (the launch's worktree).</param>
/// <param name="Block">The Step's block id.</param>
/// <param name="Run">The run's token.</param>
/// <param name="Since">When the run's session was launched: a file older than this belongs to an earlier Step that shared the run directory; null reads every file.</param>
internal sealed record AgentRecoverRun(string Worktree, string Block, string Run, DateTimeOffset? Since = null);
