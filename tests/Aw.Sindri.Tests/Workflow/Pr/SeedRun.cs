namespace Aw.Sindri.Tests.Workflow.Pr;

/// <summary>
/// What a seed's agent run left behind, as a prompt-eval case sees it (PB spec §9.3, Appendix B.2): the report's outcome and the
/// workspace changes. The deterministic 'check:' graders in <see cref="SeedGraders"/> read only this, so they need no model.
/// </summary>
internal sealed record SeedRun
{
    /// <summary>The Step's reported outcome.</summary>
    public required string Outcome { get; init; }

    /// <summary>The paths the run changed, relative to the repo root.</summary>
    public IReadOnlyList<string> ChangedPaths { get; init; } = [];

    /// <summary>The lines the run added, across all changed files.</summary>
    public IReadOnlyList<string> AddedLines { get; init; } = [];

    /// <summary>Tests the run deleted, skipped or loosened (empty when none).</summary>
    public IReadOnlyList<string> WeakenedTests { get; init; } = [];

    /// <summary>The messages of the commits the run made.</summary>
    public IReadOnlyList<string> CommitMessages { get; init; } = [];

    /// <summary>The commit the report names (for 'fix-comment', 'fact:fix.commit').</summary>
    public string? ReportedCommit { get; init; }

    /// <summary>Whether the check that failed before the fix passes on the run's commit.</summary>
    public bool FailingCheckPassesNow { get; init; }

    /// <summary>Whether the repo's unit command passes on the run's commit.</summary>
    public bool UnitCommandPasses { get; init; }

    /// <summary>The drafted reply text, when the Step drafts one.</summary>
    public string? Reply { get; init; }

    /// <summary>The links in the changed docs that do not resolve.</summary>
    public IReadOnlyList<string> UnresolvedLinks { get; init; } = [];
}
