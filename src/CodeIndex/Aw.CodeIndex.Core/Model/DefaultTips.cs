namespace Aw.CodeIndex.Core.Model;

/// <summary>Where a repo with no index gets its first build from (#3181): its head on the default branch, else the default branch's tip, else nowhere.</summary>
public static class DefaultTips
{
    /// <summary>Whether no first build can start where the repo is: its head is off the default branch and the checkout has no default-branch commit to build from.</summary>
    /// <param name="head">The repo's live head, or null when unread.</param>
    /// <returns>True when no build can start; false for an unread head.</returns>
    public static bool NoneToBuildFrom(RepoHead? head) => head is { Head: not null, OnDefaultBranch: false, DefaultTip: null };
}
