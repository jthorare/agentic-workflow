namespace Aw.Meta.Core.Worktrees;

/// <summary>Which worktrees an inspection looks at: all of them, or those that belong to some pull requests (their head branches and their gate trees).</summary>
public sealed record WorktreeScope
{
    private WorktreeScope(IReadOnlySet<int>? prs, bool includeNoPr = false)
    {
        Prs = prs;
        IncludeNoPr = includeNoPr;
    }

    /// <summary>Every worktree of the repo.</summary>
    public static WorktreeScope All { get; } = new((IReadOnlySet<int>?)null);

    /// <summary>Every worktree of the repo, and a branch with no pull request may be removed too (the sweep's --include-no-pr).</summary>
    public static WorktreeScope AllIncludingNoPr { get; } = new(prs: null, includeNoPr: true);

    /// <summary>Whether a branch with no pull request may be judged removable; otherwise every such worktree is kept.</summary>
    public bool IncludeNoPr { get; }

    /// <summary>The pull requests, or null for every worktree.</summary>
    public IReadOnlySet<int>? Prs { get; }

    /// <summary>The worktrees of some pull requests.</summary>
    /// <param name="prs">The pull request numbers.</param>
    /// <returns>The scope.</returns>
    public static WorktreeScope ForPrs(IEnumerable<int> prs)
    {
        ArgumentNullException.ThrowIfNull(prs);
        return new WorktreeScope(prs.ToHashSet());
    }
}
