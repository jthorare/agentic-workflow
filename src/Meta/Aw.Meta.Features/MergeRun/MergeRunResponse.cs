using Aw.Meta.Core.Merge;

namespace Aw.Meta.Features.MergeRun;

/// <summary>The MergeRun response.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="Merged">Whether the pull request was merged; false when a check refused it.</param>
/// <param name="Checks">The merge checks.</param>
/// <param name="Sha">The sha main moved to, when merged.</param>
/// <param name="Announce">The text to send every lane (for the log and the owner; lanes read it from the board), when merged.</param>
/// <param name="Worktrees">The worktree sweep's summary line after a merge: what was removed (or would be) and what was kept and why; null when no worktree belongs to the pull request or the sweep could not run.</param>
/// <param name="Queued">Whether GitHub's merge queue took the pull request: it is not merged yet, and the watch records the merge when it lands.</param>
/// <param name="Position">Its place in GitHub's merge queue (1 is next), when queued and GitHub told.</param>
public sealed record MergeRunResponse(int Pr, bool Merged, IReadOnlyList<MergeCheck> Checks, string? Sha, string? Announce, string? Worktrees = null, bool Queued = false, int? Position = null);
