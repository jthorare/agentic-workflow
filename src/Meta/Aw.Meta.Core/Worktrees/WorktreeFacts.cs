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

    /// <summary>Whether no removing sweep has been recorded yet, so "changed since the last sweep" has nothing to compare with.</summary>
    public bool NoPreviousSweep { get; init; }

    /// <summary>The number of the pull request whose head branch this is.</summary>
    public int? PrNumber { get; init; }

    /// <summary>That pull request's state.</summary>
    public PrState? PrState { get; init; }

    /// <summary>Whether a branch with no pull request is kept whatever else holds (a sweep without --include-no-pr).</summary>
    public bool KeepNoPr { get; init; }

    /// <summary>The pull request a gate tree belongs to; null for any other worktree.</summary>
    public int? GatePr { get; init; }

    /// <summary>Whether a gate tree's HEAD is the merge commit the gate made (committer aw-meta@localhost), whose parents are main and the pull request's head.</summary>
    public bool HeadIsGateMerge { get; init; }

    /// <summary>Whether a gate-run, gate-rerun or merge-run of the gate tree's pull request is running.</summary>
    public bool GateRunning { get; init; }
}
