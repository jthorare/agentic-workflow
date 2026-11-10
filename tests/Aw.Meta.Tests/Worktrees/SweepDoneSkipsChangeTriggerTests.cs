using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>#2544: `worktree-sweep --path --done` is the owning lane saying it is finished, so the "changed since the last full sweep" reason does not apply; every other rule still does.</summary>
public sealed class SweepDoneSkipsChangeTriggerTests
{
    // A detached leftover tree whose commits are all pushed, edited after the recorded sweep started.
    private static WorktreeFacts Changed() => new() { Path = "/wt/detached", HeadMerged = true, ChangedSinceSweep = true };

    private static void AssertKept(WorktreeVerdict verdict, string fragment)
    {
        Assert.Equal(WorktreeDecision.Kept, verdict.Decision);
        Assert.Contains(verdict.Reasons, reason => reason.Contains(fragment, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Done_removes_a_tree_changed_since_the_last_sweep()
    {
        var verdict = WorktreeRules.Judge(Changed() with { Done = true });

        Assert.Equal(WorktreeDecision.Removable, verdict.Decision);
        Assert.DoesNotContain(verdict.Reasons, reason => reason.Contains("changed since", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_done_the_change_trigger_keeps_a_changed_tree() =>
        AssertKept(WorktreeRules.Judge(Changed() with { Branch = "feat-x", PrNumber = 7, PrState = PrState.Merged }), "changed since the last full sweep");

    [Fact, Trait("Lane", "unit")]
    public void Done_still_keeps_a_tree_with_uncommitted_files() =>
        AssertKept(WorktreeRules.Judge(Changed() with { Done = true, DirtyEntries = ["NOTES.md"] }), "NOTES.md");

    [Fact, Trait("Lane", "unit")]
    public void Done_still_keeps_a_tree_with_a_process_in_it() =>
        AssertKept(WorktreeRules.Judge(Changed() with { Done = true, Occupants = [new ProcessLocation(4242, "claude -n t1-api", ["/wt/detached"])] }), "4242");
}
