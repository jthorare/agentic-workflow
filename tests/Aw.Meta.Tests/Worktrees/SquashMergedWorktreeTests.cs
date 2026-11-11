using Aw.Meta.Core.Watch;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>
/// Issue 2659: a squash merge (a GitHub merge queue's default) puts a new commit on main, so the branch head is never an ancestor of
/// origin/main. The sweep kept such a tree forever with "not merged into origin/main (PR #N is merged)". A merged pull request is
/// ended like a closed one: only commits on no remote keep its tree.
/// </summary>
public sealed class SquashMergedWorktreeTests
{
    // Clean, idle, a sweep recorded before; GitHub says merged, git says the head is not in origin/main.
    private static WorktreeFacts SquashMerged() => new() { Path = "/wt/feat-sq", Branch = "feat-sq", PrNumber = 21, PrState = PrState.Merged, HeadMerged = false };

    [Fact, Trait("Lane", "unit")]
    public void A_squash_merged_pull_request_branch_with_everything_pushed_is_removable()
    {
        var verdict = WorktreeRules.Judge(SquashMerged() with { UnpushedCommits = 0 });

        Assert.Equal(WorktreeDecision.Removable, verdict.Decision);
        Assert.Empty(verdict.Reasons);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_squash_merged_pull_request_branch_with_unpushed_commits_is_kept()
    {
        var verdict = WorktreeRules.Judge(SquashMerged() with { UnpushedCommits = 2 });

        Assert.Equal(WorktreeDecision.Kept, verdict.Decision);
        Assert.Contains(verdict.Reasons, reason => reason.Contains("PR #21 is merged", StringComparison.Ordinal) && reason.Contains("not on any remote", StringComparison.Ordinal));
    }
}
