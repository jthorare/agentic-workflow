namespace Aw.Sindri.Core.Sessions;

/// <summary>The facts one Agents row shows after its title (Agents page spec §3.1): lane, repo and branch, the PR or issue, the context size, the heavy slot. The age of the last activity is <see cref="AgentRow.LastActivity"/>.</summary>
/// <param name="Lane">The lane; for a builder, its lead's lane.</param>
/// <param name="Repo">The profile repo.</param>
/// <param name="Branch">The branch of the session's worktree.</param>
/// <param name="WorkRef">The PR or issue the session works on, such as "PR #3078".</param>
/// <param name="ContextTokens">The context size in tokens.</param>
/// <param name="ContextAtMark">True when the context is at the recycle mark.</param>
/// <param name="HeavySlot">The heavy slot the session holds or waits for, such as "2 of 3".</param>
public sealed record AgentFacts(string? Lane, string? Repo, string? Branch, string? WorkRef, int? ContextTokens, bool ContextAtMark, string? HeavySlot);
