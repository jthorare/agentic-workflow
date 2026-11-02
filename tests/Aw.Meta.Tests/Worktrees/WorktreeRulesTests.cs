using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>One test per rule, and the combinations that matter: a worktree is removable only when every rule holds, and nothing about age is in the facts at all.</summary>
public sealed class WorktreeRulesTests
{
    private static WorktreeFacts Merged() => new() { Path = "/wt/feat-x", Branch = "feat-x", PrNumber = 7, PrState = PrState.Merged };

    private static WorktreeVerdict Judge(WorktreeFacts facts) => WorktreeRules.Judge(facts);

    private static void AssertKept(WorktreeVerdict verdict, params string[] reasonFragments)
    {
        Assert.Equal(WorktreeDecision.Kept, verdict.Decision);
        foreach (var fragment in reasonFragments)
        {
            Assert.Contains(verdict.Reasons, reason => reason.Contains(fragment, StringComparison.Ordinal));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_merged_clean_idle_worktree_with_an_ended_pull_request_is_removed()
    {
        var verdict = Judge(Merged());

        Assert.Equal(WorktreeDecision.Removable, verdict.Decision);
        Assert.Empty(verdict.Reasons);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_old_but_unmerged_worktree_with_a_clean_tree_is_kept()
    {
        // Nothing in the facts says how long ago anything happened: an idle, clean branch with a commit that is on no remote can still be in progress.
        var facts = Merged() with { HeadMerged = false, PrNumber = null, PrState = null, UnpushedCommits = 1 };

        AssertKept(Judge(facts), "1 commit");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_merged_worktree_with_an_untracked_notes_file_is_kept()
    {
        var verdict = Judge(Merged() with { DirtyEntries = ["NOTES.md"] });

        AssertKept(verdict, "NOTES.md");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_merged_worktree_with_a_process_inside_is_kept()
    {
        var verdict = Judge(Merged() with { Occupants = [new ProcessLocation(4242, "claude -n t1-api", ["/wt/feat-x"])] });

        AssertKept(verdict, "4242");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_closed_pull_request_with_an_unpushed_commit_is_kept()
    {
        var facts = Merged() with { HeadMerged = false, PrState = PrState.Closed, UnpushedCommits = 1 };

        AssertKept(Judge(facts), "closed", "1 commit");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_closed_pull_request_whose_head_is_on_the_remote_is_removed()
    {
        var facts = Merged() with { HeadMerged = false, PrState = PrState.Closed, UnpushedCommits = 0 };

        Assert.Equal(WorktreeDecision.Removable, Judge(facts).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_open_pull_request_keeps_its_worktree_even_when_the_head_is_merged() => AssertKept(Judge(Merged() with { PrState = PrState.Open }), "open");

    [Fact, Trait("Lane", "unit")]
    public void Unpushed_commits_keep_a_worktree_whatever_else_holds() => AssertKept(Judge(Merged() with { UnpushedCommits = 2 }), "2 commit");

    [Fact, Trait("Lane", "unit")]
    public void A_detached_worktree_that_is_not_a_gate_tree_is_always_kept()
    {
        var detached = new WorktreeFacts { Path = "/wt/detached" };

        AssertKept(Judge(detached), "detached, not a gate tree");
        AssertKept(Judge(detached with { HeadMerged = false }), "detached, not a gate tree");
    }

    private static WorktreeFacts NoPr() => new() { Path = "/wt/idea", Branch = "docs/idea", HeadMerged = false };

    [Fact, Trait("Lane", "unit")]
    public void No_pr_branch_fully_pushed_clean_idle_is_removed()
    {
        var verdict = Judge(NoPr() with { UnpushedCommits = 0 });

        Assert.Equal(WorktreeDecision.Removable, verdict.Decision);
        Assert.Empty(verdict.Reasons);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_pr_branch_with_head_in_origin_main_is_removed()
    {
        var fresh = new WorktreeFacts { Path = "/wt/fresh", Branch = "docs/fresh", HeadMerged = true };

        Assert.Equal(WorktreeDecision.Removable, Judge(fresh).Decision);
        Assert.Equal(WorktreeDecision.Removable, Judge(NoPr() with { HeadMerged = true, UnpushedCommits = 1 }).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_pr_branch_with_unpushed_commit_is_kept() => AssertKept(Judge(NoPr() with { UnpushedCommits = 2 }), "2 commit", "no pull request");

    [Fact, Trait("Lane", "unit")]
    public void No_pr_branch_is_kept_when_there_is_no_previous_sweep()
    {
        AssertKept(Judge(NoPr() with { NoPreviousSweep = true }), "no previous sweep to compare with");
        AssertKept(Judge(new WorktreeFacts { Path = "/wt/fresh", Branch = "docs/fresh", HeadMerged = true, NoPreviousSweep = true }), "no previous sweep to compare with");
        Assert.Equal(WorktreeDecision.Removable, Judge(Merged() with { NoPreviousSweep = true }).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_pr_branch_dirty_is_kept() => AssertKept(Judge(NoPr() with { DirtyEntries = ["NOTES.md"] }), "NOTES.md");

    [Fact, Trait("Lane", "unit")]
    public void No_pr_branch_with_process_is_kept() => AssertKept(Judge(NoPr() with { Occupants = [new ProcessLocation(4242, "claude -n t1-api", ["/wt/idea"])] }), "4242");

    [Fact, Trait("Lane", "unit")]
    public void No_pr_branch_changed_since_last_sweep_is_kept() => AssertKept(Judge(NoPr() with { ChangedSinceSweep = true }), "changed since the last sweep");

    [Fact, Trait("Lane", "unit")]
    public void No_pr_branch_outside_the_roots_or_locked_is_kept()
    {
        AssertKept(Judge(NoPr() with { UnderAllowedRoot = false }), "allowed");
        AssertKept(Judge(NoPr() with { Locked = true }), "locked");
    }

    [Fact, Trait("Lane", "unit")]
    public void Open_pr_branch_is_still_kept() => AssertKept(Judge(NoPr() with { PrNumber = 5, PrState = PrState.Open, HeadMerged = true }), "PR #5 is still open");

    [Fact, Trait("Lane", "unit")]
    public void The_main_checkout_is_kept_as_the_main_checkout_not_as_a_branch_with_no_pull_request()
    {
        var verdict = Judge(new WorktreeFacts { Path = "/repo", Branch = "main", IsMainWorktree = true, HeadMerged = true });

        AssertKept(verdict, "this is the main worktree", "main checkout (never swept)");
        Assert.DoesNotContain(verdict.Reasons, reason => reason.Contains("no pull request", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tree_changed_after_the_last_sweep_is_kept() => AssertKept(Judge(Merged() with { ChangedSinceSweep = true }), "changed since the last sweep");

    [Fact, Trait("Lane", "unit")]
    public void A_gate_tree_is_not_held_back_by_the_change_trigger()
    {
        var gate = new WorktreeFacts { Path = "/gates/7/tree", GatePr = 7, PrState = PrState.Merged, HeadMerged = true, ChangedSinceSweep = true };

        Assert.Equal(WorktreeDecision.Removable, Judge(gate).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_main_worktree_the_shared_checkout_and_a_locked_worktree_are_never_removed()
    {
        AssertKept(Judge(Merged() with { IsMainWorktree = true }), "main worktree");
        AssertKept(Judge(Merged() with { IsSharedCheckout = true }), "shared checkout");
        AssertKept(Judge(Merged() with { Locked = true, LockReason = "usb disk" }), "locked", "usb disk");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_worktree_outside_the_allowed_roots_is_kept() => AssertKept(Judge(Merged() with { UnderAllowedRoot = false }), "allowed");

    [Fact, Trait("Lane", "unit")]
    public void A_worktree_whose_directory_is_gone_is_kept_for_git_to_prune() => AssertKept(Judge(Merged() with { DirectoryMissing = true }), "directory is missing");

    [Fact, Trait("Lane", "unit")]
    public void Every_failing_rule_is_reported_not_just_the_first()
    {
        var facts = Merged() with
        {
            HeadMerged = false,
            PrState = PrState.Open,
            DirtyEntries = ["a.cs", "b.cs"],
            UnpushedCommits = 3,
            Occupants = [new ProcessLocation(1, "vim", ["/wt/feat-x/a.cs"])],
            UnderAllowedRoot = false,
        };

        var verdict = Judge(facts);

        Assert.Equal(WorktreeDecision.Kept, verdict.Decision);
        Assert.True(verdict.Reasons.Count >= 5, string.Join(" | ", verdict.Reasons));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_gate_tree_of_an_ended_pull_request_whose_head_is_the_gate_merge_is_removed()
    {
        var gate = new WorktreeFacts { Path = "/gates/7/tree", GatePr = 7, PrNumber = null, PrState = PrState.Merged, HeadMerged = false, HeadIsGateMerge = true, UnpushedCommits = 1 };

        Assert.Equal(WorktreeDecision.Removable, Judge(gate).Decision);
        Assert.Equal(WorktreeDecision.Removable, Judge(gate with { PrState = PrState.Closed }).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_gate_tree_of_an_open_unknown_or_running_gate_is_kept()
    {
        var gate = new WorktreeFacts { Path = "/gates/7/tree", GatePr = 7, PrState = PrState.Merged, HeadMerged = true };

        AssertKept(Judge(gate with { PrState = PrState.Open }), "#7", "open");
        AssertKept(Judge(gate with { PrState = null }), "#7", "unknown");
        AssertKept(Judge(gate with { GateRunning = true }), "gate", "running");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_gate_tree_whose_head_is_neither_merged_nor_the_gates_own_merge_is_kept()
    {
        var gate = new WorktreeFacts { Path = "/gates/7/tree", GatePr = 7, PrState = PrState.Merged, HeadMerged = false, HeadIsGateMerge = false, UnpushedCommits = 1 };

        AssertKept(Judge(gate), "gate tree");
    }
}
