using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Core.Worktrees;

/// <summary>Everything the rules need to know about one worktree, gathered by the inspector. Defaults describe an ordinary clean, merged branch.</summary>
internal sealed record WorktreeFacts
{
    /// <summary>The worktree's directory.</summary>
    public required string Path { get; init; }

    /// <summary>The branch checked out; null when HEAD is detached.</summary>
    public string? Branch { get; init; }

    /// <summary>The main worktree (git lists it first).</summary>
    public bool IsMainWorktree { get; init; }

    /// <summary>The team's shared checkout.</summary>
    public bool IsSharedCheckout { get; init; }

    /// <summary>Whether "git worktree lock" was run on it.</summary>
    public bool Locked { get; init; }

    /// <summary>The lock's reason.</summary>
    public string? LockReason { get; init; }

    /// <summary>Whether its directory is gone.</summary>
    public bool DirectoryMissing { get; init; }

    /// <summary>Whether it is under one of the allowed roots.</summary>
    public bool UnderAllowedRoot { get; init; } = true;

    /// <summary>Whether its HEAD is reachable from origin/main.</summary>
    public bool HeadMerged { get; init; } = true;

    /// <summary>How many commits of its HEAD are on no remote branch.</summary>
    public int UnpushedCommits { get; init; }

    /// <summary>Uncommitted or untracked paths, build output already left out.</summary>
    public IReadOnlyList<string> DirtyEntries { get; init; } = [];

    /// <summary>The live processes with their working directory or an open file inside it.</summary>
    public IReadOnlyList<ProcessLocation> Occupants { get; init; } = [];

    /// <summary>Whether the directory or one of its files (top two levels, build output left out) changed after the last sweep started.</summary>
    public bool ChangedSinceSweep { get; init; }

    /// <summary>Whether <see cref="ChangedSinceSweep"/> was judged against when the tree's merged or closed pull request ended, rather than against the last sweep's start.</summary>
    public bool ChangedSinceEnd { get; init; }

    /// <summary>Whether the tree's pull request merged or closed but GitHub gave no time for it, so "changed since" can't be judged (the tree is kept).</summary>
    public bool EndedAtUnknown { get; init; }

    /// <summary>When the last full sweep started (the cutoff a tree with no ended pull request is compared with); null when none has been recorded.</summary>
    public DateTimeOffset? SweepStartedAt { get; init; }

    /// <summary>Whether no removing sweep has been recorded yet, so "changed since the last full sweep" has nothing to compare with.</summary>
    public bool NoPreviousSweep { get; init; }

    /// <summary>The number of the pull request whose head branch this is.</summary>
    public int? PrNumber { get; init; }

    /// <summary>That pull request's state.</summary>
    public PrState? PrState { get; init; }

    /// <summary>The worktree's HEAD commit.</summary>
    public string Head { get; init; } = string.Empty;

    /// <summary>The head commit GitHub records for that pull request (for an ended one, its head when it ended); empty when unknown.</summary>
    public string PrHead { get; init; } = string.Empty;

    /// <summary>How many commits HEAD has that <see cref="PrHead"/> lacks (0 when HEAD is that head or behind it); null when git could not tell.</summary>
    public int? CommitsAfterPrHead { get; init; }

    /// <summary>Set by a `--discarded` sweep when a finished bugfix run names this tree as a losing candidate at its HEAD: says which (e.g. "bugfix gh-9 candidate c1, resolved by c2"). Its unpushed commits and "no pull request" don't keep it.</summary>
    public string? Discarded { get; init; }

    /// <summary>Set by any sweep when a bugfix run names this tree at its HEAD (its red tree, or a candidate on its branch) and the run's GitHub issue is closed by a merged pull request: says which (e.g. "bugfix gh-9 red tree, #9 closed by merged #12"). "No pull request", "unmerged branch", unpushed commits and being detached don't keep it; the live-tree rules still do (#2753).</summary>
    public string? FinishedBugfix { get; init; }

    /// <summary>Set by a `--discarded` sweep when the tree is not a discarded candidate: why not (no run names it, HEAD moved, it won, the run isn't over).</summary>
    public string? NotDiscarded { get; init; }

    /// <summary>Set by a `--done` sweep: the owning lane says it is finished with this tree, so a detached tree is judged by the other rules instead of being kept for being detached.</summary>
    public bool Done { get; init; }

    /// <summary>Whether a branch with no pull request is kept whatever else holds (a sweep without --include-no-pr).</summary>
    public bool KeepNoPr { get; init; }

    /// <summary>The pull request a gate tree belongs to; null for any other worktree.</summary>
    public int? GatePr { get; init; }

    /// <summary>Whether a gate tree's HEAD is the merge commit the gate made (committer aw-meta@localhost), whose parents are main and the pull request's head.</summary>
    public bool HeadIsGateMerge { get; init; }

    /// <summary>Whether an aw-meta process is running gate-run, gate-rerun or merge-run for the gate tree's pull request (a command line that only mentions them doesn't count).</summary>
    public bool GateRunning { get; init; }
}
