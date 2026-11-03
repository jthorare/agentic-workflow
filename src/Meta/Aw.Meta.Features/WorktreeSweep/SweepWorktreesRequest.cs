namespace Aw.Meta.Features.WorktreeSweep;

/// <summary>The SweepWorktrees request: remove the removable worktrees.</summary>
/// <param name="DryRun">Report what would be removed; remove nothing.</param>
/// <param name="Pr">Only the worktrees of this pull request (its head branch and its gate trees); null for every worktree.</param>
/// <param name="IncludeNoPr">Also remove a branch with no pull request when every other rule holds; without it such a worktree is always kept.</param>
/// <param name="Path">Only this worktree; null for every worktree.</param>
/// <param name="Discarded">With <paramref name="Path"/>: the tree may be removed although its commits are unpushed, when a finished bugfix run names it as a losing candidate at its HEAD.</param>
public sealed record SweepWorktreesRequest(bool DryRun = false, int? Pr = null, bool IncludeNoPr = false, string? Path = null, bool Discarded = false);
