using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>
/// Issue 2753: a tree a finished bugfix run names (its issue closed by a merged PR) is finished work. "No PR", "unmerged branch",
/// unpushed commits and being detached don't keep it; dirty, in use, locked, outside the roots, changed since the last sweep and no
/// previous sweep still do.
/// </summary>
public sealed class FinishedBugfixRulesTests
{
    private const string Finished = "bugfix gh-9 red tree, #9 closed by merged #12";

    private static WorktreeFacts Tree() => new()
    {
        Path = "/wt/gh-9",
        Branch = "bugfix/gh-9",
        HeadMerged = false,
        UnpushedCommits = 1,
        PrState = null,
        KeepNoPr = true,
        FinishedBugfix = Finished,
    };

    [Fact, Trait("Lane", "unit")]
    public void A_finished_bugfix_tree_with_no_pr_and_an_unpushed_commit_is_removable()
    {
        var verdict = WorktreeRules.Judge(Tree());

        Assert.Equal(WorktreeDecision.Removable, verdict.Decision);
        Assert.Empty(verdict.Reasons);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_detached_finished_bugfix_tree_is_removable() =>
        Assert.Equal(WorktreeDecision.Removable, WorktreeRules.Judge(Tree() with { Branch = null }).Decision);

    [Fact, Trait("Lane", "unit")]
    public void Without_the_fact_the_same_tree_is_kept() =>
        Assert.Equal(WorktreeDecision.Kept, WorktreeRules.Judge(Tree() with { FinishedBugfix = null }).Decision);

    [Fact, Trait("Lane", "unit")]
    public void The_live_tree_rules_still_keep_it()
    {
        WorktreeFacts[] live =
        [
            Tree() with { DirtyEntries = ["notes.txt"] },
            Tree() with { ChangedSinceSweep = true },
            Tree() with { NoPreviousSweep = true },
            Tree() with { Locked = true },
            Tree() with { UnderAllowedRoot = false },
            Tree() with { Occupants = [new(42, "dotnet test", ["/wt/gh-9"], IsInvoker: false)] },
        ];

        Assert.All(live, facts => Assert.Equal(WorktreeDecision.Kept, WorktreeRules.Judge(facts).Decision));
    }
}
