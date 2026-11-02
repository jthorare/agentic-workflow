using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'RecoverAgentStep' is given (issue #1000): the run directory the ended session wrote to, the Step's block, and the run's token (it keys the recovered report).</summary>
/// <param name="Worktree">The launch's worktree: where outputs are read from when <paramref name="RunDirectory"/> is null (a run launched before it existed).</param>
/// <param name="Block">The Step's block id.</param>
/// <param name="Run">The run's token.</param>
/// <param name="Since">When the run's session was launched: a file older than this belongs to an earlier Step that shared the run directory; null reads every file.</param>
/// <param name="Payloads">The payload facts the Step writes: a recovered output for one of them must be a JSON object or array, as <c lang="text">complete</c> demands; null checks none.</param>
/// <param name="RunDirectory">The Step's run directory (AgentLaunched.RunDirectory); the output folder is .sindri-out under it. Null reads the worktree.</param>
internal sealed record AgentRecoverRun(string Worktree, string Block, string Run, DateTimeOffset? Since = null, IReadOnlyList<FactKey>? Payloads = null, string? RunDirectory = null)
{
    /// <summary>Where the output folder is: the run directory, else the worktree.</summary>
    public string Root => RunDirectory ?? Worktree;
}
