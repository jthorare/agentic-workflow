namespace Aw.BugFix.Core.Model;

/// <summary>What the helper asks judge resolution-check, built from state only (never from agent-written text).</summary>
/// <param name="Brief">The ticket brief, verbatim.</param>
/// <param name="Expected">The expected behaviour.</param>
/// <param name="Actual">The observed behaviour.</param>
/// <param name="RootCause">The snapshotted root cause.</param>
/// <param name="CheckKind">The frozen check's kind.</param>
/// <param name="CheckSummary">A description of the frozen check, derived from state.</param>
/// <param name="BeforePassed">Whether the check passed on the unfixed code (false: advance reproduce requires a failure).</param>
/// <param name="AfterPassed">Whether the check passed on the candidate (true: judge requires it).</param>
/// <param name="DiffStat">git diff --stat from the baseline to the candidate.</param>
/// <param name="Diff">The diff (lock files and build output excluded), capped; null when git couldn't produce it.</param>
/// <param name="CheckSource">The frozen check's own file as it is at the candidate's commit, capped; null when git couldn't read it. For a .csproj check: the test class files its --filter-class names, as they are at the baseline commit.</param>
/// <param name="BaselineFailure">The failure lines of the baseline run's log (capped), so the judge sees what the check printed on the unfixed code; null when the log has none or couldn't be read.</param>
public sealed record ResolutionCheckInput(
    string Brief,
    string Expected,
    string Actual,
    string RootCause,
    CheckKind CheckKind,
    string CheckSummary,
    bool BeforePassed,
    bool AfterPassed,
    string DiffStat,
    string? Diff,
    string? CheckSource = null,
    string? BaselineFailure = null)
{
    /// <summary>The most diff characters the input carries; longer diffs are cut and marked.</summary>
    public const int DiffCap = 1_000_000;

    /// <summary>The most failure-line characters the input carries.</summary>
    public const int FailureCap = 8_000;
}
