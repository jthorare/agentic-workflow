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
        IReadOnlySet<string> checkedOutBranches)
    {
        ArgumentNullException.ThrowIfNull(prs);
        ArgumentNullException.ThrowIfNull(queued);
        ArgumentNullException.ThrowIfNull(ready);
        ArgumentNullException.ThrowIfNull(checkedOutBranches);
        var byPrefix = prefixes is { Count: > 0 } ? prefixes : null;
        var owned = new HashSet<int>(prs.Where(pr => queued.Contains(pr.Number) || ready.Contains(pr.Number) || BranchIsTeams(pr.HeadBranch, byPrefix, checkedOutBranches)).Select(pr => pr.Number));
        // A pull request stacked on an owned one is owned too, however deep the stack goes.
        bool grew;
        do
        {
            var heads = prs.Where(pr => owned.Contains(pr.Number) && pr.HeadBranch.Length > 0).Select(pr => pr.HeadBranch).ToHashSet(StringComparer.Ordinal);
            grew = false;
            foreach (var pr in prs)
            {
                if (pr.BaseBranch.Length > 0 && heads.Contains(pr.BaseBranch) && owned.Add(pr.Number))
                {
                    grew = true;
                }
            }
        }
        while (grew);

        return [.. prs.Where(pr => owned.Contains(pr.Number))];
    }

    private static bool BranchIsTeams(string head, IReadOnlyList<string>? prefixes, IReadOnlySet<string> checkedOut) =>
        head.Length > 0 && (prefixes is null ? checkedOut.Contains(head) : prefixes.Any(prefix => head.StartsWith(prefix, StringComparison.Ordinal)));
}
