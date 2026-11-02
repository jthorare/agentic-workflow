using System.Globalization;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Core.Worktrees;

/// <summary>
/// The rules that decide whether a worktree may be removed. All must hold, so every failing one is reported. The work must be
/// proven finished: an ended pull request, or, with no pull request, everything pushed or in origin/main (a live tree on a fresh branch is protected by rules 4 and 7).
/// Nothing about age is among them (a worktree untouched for weeks may still be work in progress); the one time rule is a change
/// trigger, "changed since the last sweep".
/// </summary>
internal static class WorktreeRules
{
    private const int ListedPaths = 3;

    /// <summary>Judges one worktree.</summary>
    /// <param name="facts">What the inspector found.</param>
    /// <returns>Removable when no rule fails; otherwise kept, with every failing rule's reason.</returns>
    internal static WorktreeVerdict Judge(WorktreeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        List<string> reasons = [];
        Protected(facts, reasons);
        if (facts.GatePr is { } gate)
        {
            GateTree(facts, gate, reasons);
        }
        else
        {
            Merged(facts, reasons);
            if (facts.UnpushedCommits > 0 && !(facts.PrState is null && facts.HeadMerged))
            {
                reasons.Add(string.Create(CultureInfo.InvariantCulture, $"{facts.UnpushedCommits} commit(s) are on no remote branch"));
            }
        }

        Untouched(facts, reasons);
        return new WorktreeVerdict(reasons.Count == 0 ? WorktreeDecision.Removable : WorktreeDecision.Kept, reasons);
    }

    // Rules 5 and 6: never the main worktree, the shared checkout, a locked one, one whose directory is gone, one outside the roots.
    private static void Protected(WorktreeFacts facts, List<string> reasons)
    {
        if (facts.IsMainWorktree)
        {
            reasons.Add("this is the main worktree");
        }

        if (facts.IsSharedCheckout)
        {
            reasons.Add("this is the team's shared checkout");
        }

        if (facts.Locked)
        {
            reasons.Add(string.IsNullOrWhiteSpace(facts.LockReason) ? "locked (git worktree lock)" : "locked (git worktree lock): " + facts.LockReason);
        }

        if (facts.DirectoryMissing)
        {
            reasons.Add("its directory is missing (git worktree prune clears the entry)");
        }

        if (!facts.UnderAllowedRoot)
        {
            reasons.Add("it is outside the allowed worktree roots");
        }
    }

    // The main checkout is not a pull request branch; calling it "no pull request" reads like unfinished work.
    private static string NoPullRequest(WorktreeFacts facts) => facts.IsMainWorktree ? "main checkout" : "no pull request";

    // A branch with no pull request is finished when everything of it is safe elsewhere (its head is in origin/main, or no commit is unpushed); idle and unchanged-since-the-last-sweep still guard a live tree.
    private static void NoPullRequestReasons(WorktreeFacts facts, List<string> reasons)
    {
        if (facts.IsMainWorktree)
        {
            reasons.Add("main checkout (never swept)");
        }
        else
        {
            // A fresh branch cut from main looks finished (head in origin/main, nothing inside at this instant); only an explicit --include-no-pr sweep may remove one.
            if (facts.KeepNoPr)
            {
                reasons.Add("kept: no PR (pass --include-no-pr to remove)");
            }

            if (!facts.HeadMerged && facts.UnpushedCommits > 0)
            {
                reasons.Add("no pull request: work not finished (not all of it is pushed)");
            }

            // Rule 7 needs a recorded sweep; without one a live tree on a fresh branch would look unchanged.
            if (facts.NoPreviousSweep)
            {
                reasons.Add("no previous sweep to compare with");
                if (!facts.HeadMerged)
                {
                    reasons.Add("not merged into origin/main (no pull request)");
                }
            }
        }
    }

    // Rule 1: a detached worktree that is not a gate tree is never removed; a branch with a pull request only when it ended (merged, or closed with everything on the remote); a branch with none when everything of it is pushed or in origin/main.
    private static void Merged(WorktreeFacts facts, List<string> reasons)
    {
        if (facts.Branch is null)
        {
            reasons.Add("detached, not a gate tree");
            return;
        }

        var pr = facts.PrNumber is { } number ? string.Create(CultureInfo.InvariantCulture, $"PR #{number}") : NoPullRequest(facts);
        if (facts.PrState == PrState.Open)
        {
            reasons.Add(pr + " is still open");
        }

        if (facts.PrState is null)
        {
            NoPullRequestReasons(facts, reasons);
        }

        if (facts.HeadMerged || facts.PrState is null)
        {
            return;
        }

        if (facts.PrState == PrState.Closed)
        {
            if (facts.UnpushedCommits > 0)
            {
                reasons.Add($"{pr} is closed but its head is not merged into origin/main and not on any remote");
            }
        }
        else
        {
            reasons.Add($"not merged into origin/main ({pr}{(facts.PrState is { } state ? " is " + state.ToString().ToLowerInvariant() : string.Empty)})");
        }
    }

    // A gate tree is aw-meta's own throwaway checkout: removable once its pull request ended, no gate run is using it, and its head holds nothing but the gate's merge.
    private static void GateTree(WorktreeFacts facts, int gate, List<string> reasons)
    {
        var name = string.Create(CultureInfo.InvariantCulture, $"gate tree of #{gate}");
        if (facts.PrState is null)
        {
            reasons.Add($"{name}: the pull request's state is unknown");
        }
        else if (facts.PrState == PrState.Open)
        {
            reasons.Add($"{name}: its pull request is still open");
        }

        if (facts.GateRunning)
        {
            reasons.Add(string.Create(CultureInfo.InvariantCulture, $"a gate run for #{gate} is running"));
        }

        if (!facts.HeadMerged && !facts.HeadIsGateMerge)
        {
            reasons.Add("gate tree: HEAD is neither merged into origin/main nor the gate's own merge commit");
        }
    }

    // Rules 2 and 4 and the change trigger: nothing uncommitted or untracked beyond build output, no process in it, and (for a worktree that is not a gate tree) nothing changed since the last sweep started.
    private static void Untouched(WorktreeFacts facts, List<string> reasons)
    {
        if (facts.ChangedSinceSweep && facts.GatePr is null)
        {
            reasons.Add("changed since the last sweep");
        }

        if (facts.DirtyEntries.Count > 0)
        {
            var more = facts.DirtyEntries.Count > ListedPaths ? string.Create(CultureInfo.InvariantCulture, $" (+{facts.DirtyEntries.Count - ListedPaths} more)") : string.Empty;
            reasons.Add("uncommitted or untracked files: " + string.Join(", ", facts.DirtyEntries.Take(ListedPaths)) + more);
        }

        foreach (var process in facts.Occupants.Take(ListedPaths))
        {
            reasons.Add(string.Create(CultureInfo.InvariantCulture, $"in use by pid {process.Pid} ({Short(process.Command)})"));
        }

        if (facts.Occupants.Count > ListedPaths)
        {
            reasons.Add(string.Create(CultureInfo.InvariantCulture, $"in use by {facts.Occupants.Count - ListedPaths} more process(es)"));
        }
    }

    private static string Short(string command) => command.Length > 60 ? command[..60] + "..." : command;
}
