using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>
/// Issue 2005: a losing /bugFixOrchestrator candidate's fix commit is never pushed, so "work not finished" kept its tree forever. A
/// `--discarded` sweep waives only the unpushed and no-pull-request reasons for a tree the bugfix run names as a loser at its HEAD.
/// </summary>
public sealed class DiscardedCandidateRulesTests
{
    // A losing candidate: its own branch, one unpushed commit, no pull request, idle, a sweep recorded before.
    private static WorktreeFacts Loser() => new()
    {
        Path = "/wt/engine-9-a1",
        Branch = "bugfix/gh-9-a1",
        HeadMerged = false,
        UnpushedCommits = 1,
        PrState = null,
        KeepNoPr = true,
    };

    [Fact, Trait("Lane", "unit")]
    public void A_discarded_candidate_with_an_unpushed_commit_is_removable()
    {
        var verdict = WorktreeRules.Judge(Loser() with { Discarded = "bugfix gh-9 candidate c1, resolved by c2" });

        Assert.Equal(WorktreeDecision.Removable, verdict.Decision);
        Assert.Empty(verdict.Reasons);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_the_match_the_same_tree_is_kept_for_unfinished_work()
    {
        var verdict = WorktreeRules.Judge(Loser());

        Assert.Equal(WorktreeDecision.Kept, verdict.Decision);
        Assert.Contains(verdict.Reasons, reason => reason.Contains("work not finished", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_discarded_candidate_is_kept_when_dirty_but_not_for_the_sweep_window()
    {
        var dirty = WorktreeRules.Judge(Loser() with { Discarded = "c1", DirtyEntries = ["notes.txt"] });
        var changed = WorktreeRules.Judge(Loser() with { Discarded = "c1", ChangedSinceSweep = true });
        var first = WorktreeRules.Judge(Loser() with { Discarded = "c1", NoPreviousSweep = true });

        Assert.Equal(WorktreeDecision.Kept, dirty.Decision);
        Assert.DoesNotContain(dirty.Reasons, reason => reason.Contains("work not finished", StringComparison.Ordinal));
        Assert.Equal(WorktreeDecision.Removable, changed.Decision);
        Assert.Equal(WorktreeDecision.Removable, first.Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_discarded_candidate_is_still_kept_when_locked()
    {
        var verdict = WorktreeRules.Judge(Loser() with { Discarded = "c1", Locked = true });

        Assert.Equal(WorktreeDecision.Kept, verdict.Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public void When_discarded_does_not_apply_the_reason_says_why()
    {
        var verdict = WorktreeRules.Judge(Loser() with { NotDiscarded = "HEAD moved past the evaluated commit" });

        Assert.Equal(WorktreeDecision.Kept, verdict.Decision);
        Assert.Contains(verdict.Reasons, reason => string.Equals(reason, "--discarded: HEAD moved past the evaluated commit", StringComparison.Ordinal));
        Assert.Contains(verdict.Reasons, reason => reason.Contains("work not finished", StringComparison.Ordinal));
    }
}
