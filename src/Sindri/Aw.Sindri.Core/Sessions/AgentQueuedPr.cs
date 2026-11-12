namespace Aw.Sindri.Core.Sessions;

/// <summary>A PR in the merge queue that one lane sent (Agents page spec §3.2); the caller joins the entry to the lane through <c lang="text">status --json</c> <c lang="text">lanes</c>.</summary>
/// <param name="Number">The PR number.</param>
/// <param name="Gate">The gate's result.</param>
/// <param name="InGithubQueue">True when Meta already put the PR in GitHub's merge queue: nothing is left for the owner.</param>
/// <param name="ReadyAt">When the lane sent its ready message.</param>
public sealed record AgentQueuedPr(int Number, AgentGate Gate, bool InGithubQueue, DateTimeOffset ReadyAt);
