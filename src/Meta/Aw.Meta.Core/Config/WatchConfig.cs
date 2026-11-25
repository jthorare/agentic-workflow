namespace Aw.Meta.Core.Config;

/// <summary>The watch's settings.</summary>
/// <param name="PrBranchPrefixes">Head-branch prefixes that mark a pull request as the team's; null means the branches checked out in the team's worktrees.</param>
public sealed record WatchConfig(IReadOnlyList<string>? PrBranchPrefixes = null);
