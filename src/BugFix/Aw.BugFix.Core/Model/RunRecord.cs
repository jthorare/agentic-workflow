using Aw.BugFix.Core.Errors;
using Aw.Kernel;

namespace Aw.BugFix.Core.Model;

/// <summary>The evidence file run-test and run-ui write: one run of the check, classified.</summary>
/// <param name="Kind">Test or ui-evidence.</param>
/// <param name="Outcome">Passed, failed or broken (broken: ui steps broke and none failed).</param>
/// <param name="Commit">The commit the run executed against.</param>
/// <param name="Directory">The working directory the run executed in, relative to the repository root ("" for the root).</param>
/// <param name="CheckSha256">SHA-256 of the check file the run executed.</param>
/// <param name="Command">The test command (argv); null for ui-evidence.</param>
/// <param name="ProgramSha256">The SHA-256 of the program the test command ran (after the PATH lookup); empty for ui-evidence.</param>
/// <param name="Environment">The variables that decide which code the test command ran (PATH, HOME, …), each as a digest of its value; empty for ui-evidence.</param>
/// <param name="ExitCode">The test command's or ui-evidence's exit code.</param>
/// <param name="Output">The run's log (test) or ui-evidence run directory.</param>
/// <param name="Leftovers">The untracked files in the work tree after the run (repository-relative path → SHA-256): what it left behind (.coverage, a junit report), which a later step in the same checkout accepts while unchanged, if the run was of the frozen check and the file is no protected or shadowing file.</param>
/// <param name="Checkout">The root of the work tree (checkout) the run executed in.</param>
public sealed record RunRecord(
    CheckKind Kind,
    RunOutcome Outcome,
    string Commit,
    string Directory,
    string CheckSha256,
    IReadOnlyList<string>? Command,
    string ProgramSha256,
    IReadOnlyDictionary<string, string> Environment,
    int ExitCode,
    string Output,
    IReadOnlyDictionary<string, string> Leftovers,
    string Checkout)
{
    /// <summary>Accepts this run as the baseline: it must fail. A check that passes can never prove a fix, and a broken run (it didn't start, timed out, or only had broken ui steps) is not a reproduction.</summary>
    /// <returns>The run, or AW-BUGFIX-007.</returns>
    public Result<RunRecord> AsBaseline() => Outcome switch
    {
        RunOutcome.Failed => this,
        RunOutcome.Passed => BugFixErrors.NotReproduced.With(
            "the baseline check passed on the unfixed code; the bug is not reproduced, so this check cannot prove a fix"),
        RunOutcome.Broken => BugFixErrors.NotReproduced.With(
            "the baseline run is broken (it didn't start, timed out, was killed by a signal, left an empty log, or only had broken steps, e.g. a bad selector); that is not a reproduction, so repair it and re-run"),
        _ => throw new ArgumentOutOfRangeException(nameof(Outcome), Outcome, "not an outcome"),
    };
}
