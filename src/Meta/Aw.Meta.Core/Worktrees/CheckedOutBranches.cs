namespace Aw.Meta.Core.Worktrees;

/// <summary>The branches checked out in worktrees under a set of roots, read from "git worktree list --porcelain".</summary>
public static class CheckedOutBranches
{
    /// <summary>Lists the branch of every worktree strictly inside one of the roots; detached worktrees are left out.</summary>
    /// <param name="porcelain">The output of "git worktree list --porcelain".</param>
    /// <param name="roots">The allowed worktree roots.</param>
    /// <returns>The branch names (without refs/heads/).</returns>
    public static IReadOnlySet<string> Under(string porcelain, IReadOnlyList<string> roots)
    {
        ArgumentNullException.ThrowIfNull(porcelain);
        ArgumentNullException.ThrowIfNull(roots);
        return PorcelainWorktrees.Parse(porcelain)
            .Where(entry => WorktreeRoots.Contains(roots, entry.Path))
            .Select(entry => entry.Branch ?? string.Empty)
            .Where(branch => branch.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }
}
