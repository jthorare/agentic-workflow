namespace Aw.Judge.Core.Questions;

/// <summary>resolution-check's input: a bug ticket, the check that failed before the fix and passes after it, and the fix.</summary>
/// <param name="Brief">The ticket as reported (must not be blank).</param>
/// <param name="Expected">The expected behaviour.</param>
/// <param name="Actual">The behaviour before the fix.</param>
/// <param name="RootCause">The confirmed root cause.</param>
/// <param name="CheckKind">What kind of check it is.</param>
/// <param name="CheckSummary">What the check does.</param>
/// <param name="BeforePassed">Whether the check passed on the unfixed code.</param>
/// <param name="AfterPassed">Whether the check passes with the fix.</param>
/// <param name="DiffStat">The fix's diff stat.</param>
/// <param name="Diff">The fix's full diff, when the caller sends it.</param>
/// <param name="CheckSource">The check's own file as the fix has it, when the caller sends it.</param>
/// <param name="BaselineFailure">What the check printed when it failed on the unfixed code (its failure lines), when the caller sends it.</param>
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
    string? Diff = null,
    string? CheckSource = null,
    string? BaselineFailure = null);
