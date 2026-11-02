using Aw.Meta.Core.Watch;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>
/// Issue 1403: a branch with no pull request looks finished the moment it is cut from main (its head is in origin/main) and no process
/// is inside right then, so a sweep removed live lane trees. A no-PR worktree is kept unless the sweep is asked to include them.
/// </summary>
public sealed class NoPrWorktreeKeptTests
{
    // Clean, idle, head in origin/main, a sweep recorded before: every other rule holds.
    private static WorktreeFacts NoPr() => new() { Path = "/wt/idea", Branch = "docs/idea", HeadMerged = true, PrNumber = null, PrState = null };

    [Fact, Trait("Lane", "unit")]
    public void A_no_pr_branch_whose_head_is_in_origin_main_is_kept_and_the_reason_says_no_pr()
    {
        var verdict = WorktreeRules.Judge(NoPr() with { KeepNoPr = true });

        Assert.Equal(WorktreeDecision.Kept, verdict.Decision);
        Assert.Contains(verdict.Reasons, reason => reason.Contains("no PR", StringComparison.Ordinal) && reason.Contains("--include-no-pr", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void With_include_no_pr_the_same_worktree_is_removable()
    {
        var verdict = WorktreeRules.Judge(NoPr() with { KeepNoPr = false });

        Assert.Equal(WorktreeDecision.Removable, verdict.Decision);
        Assert.Empty(verdict.Reasons);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_merged_pull_request_branch_is_still_removable_when_no_pr_worktrees_are_kept()
    {
        var merged = new WorktreeFacts { Path = "/wt/feat-x", Branch = "feat-x", PrNumber = 7, PrState = PrState.Merged, KeepNoPr = true };

        Assert.Equal(WorktreeDecision.Removable, WorktreeRules.Judge(merged).Decision);
    }
}
