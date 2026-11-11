namespace Aw.Meta.Core.Watch;

/// <summary>Decides which pull requests the team owns, so the watch reports only those.</summary>
public static class TeamPrs
{
    /// <summary>Keeps the pull requests the team owns.</summary>
    /// <param name="prs">Every pull request the poll saw.</param>
    /// <param name="queued">Numbers in the merge queue (merge.queued).</param>
    /// <param name="ready">Numbers a lane sent as "ready #N".</param>
    /// <param name="prefixes">Head-branch prefixes; null means use the checked-out branches.</param>
    /// <param name="checkedOutBranches">Branches checked out in a worktree under the team's roots.</param>
    /// <returns>The owned pull requests, stacked ones included.</returns>
    public static IReadOnlyList<PrView> Owned(
        IReadOnlyList<PrView> prs,
        IReadOnlySet<int> queued,
        IReadOnlySet<int> ready,
        IReadOnlyList<string>? prefixes,
        IReadOnlySet<string> checkedOutBranches) =>
        throw new NotSupportedException("TeamPrs.Owned is not implemented (#2771).");
}
