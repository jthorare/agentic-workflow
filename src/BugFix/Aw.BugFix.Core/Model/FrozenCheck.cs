using Aw.BugFix.Core.Errors;
using Aw.Kernel;

namespace Aw.BugFix.Core.Model;

/// <summary>The check that reproduced the bug, frozen at advance reproduce: every later run must execute exactly it.</summary>
/// <param name="Kind">A regression test or a ui-evidence script.</param>
/// <param name="Path">The check file, relative to the repository root, with '/'.</param>
/// <param name="Sha256">The check file's SHA-256 at the baseline.</param>
/// <param name="Command">The test command (argv) of the baseline run; null for a ui-evidence check.</param>
/// <param name="Directory">The working directory of the baseline run, relative to the repository root ("" for the root).</param>
/// <param name="ProgramSha256">The SHA-256 of the program the baseline command ran: a later run must run the same program.</param>
/// <param name="Environment">The variables that decided which code the baseline ran (PATH, HOME, …): each as a digest of its value: a later run must run with the same values.</param>
/// <param name="CommandFiles">Repository files the baseline command names besides the check (its runner, wrapper scripts): a fix may not change them without approval.</param>
public sealed record FrozenCheck(CheckKind Kind, string Path, string Sha256, IReadOnlyList<string>? Command, string Directory, string ProgramSha256, IReadOnlyDictionary<string, string> Environment, IReadOnlyList<string> CommandFiles)
{
    /// <summary>Accepts a later run only if it executed this check: same kind, a verdict (not broken), and for a test the same command in the same directory.</summary>
    /// <param name="record">The run.</param>
    /// <returns>The run, or AW-BUGFIX-014.</returns>
    public Result<RunRecord> Accepts(RunRecord record)
    {
        if (record.Kind != Kind)
        {
            return BugFixErrors.RunRefused.With($"the baseline check was a {Names.Of(Kind)}, this evidence is a {Names.Of(record.Kind)}");
        }

        if (record.Outcome == RunOutcome.Broken)
        {
            return BugFixErrors.RunRefused.With("the run is broken (it didn't start, timed out, was killed by a signal, left an empty log, or only had broken steps); repair it and re-run, a broken run is not a verdict on the fix");
        }

        return Kind == CheckKind.Test ? SameTestRun(record) : record;
    }

    /// <summary>Whether <paramref name="record"/> ran this check: a test run of the same file that <see cref="Accepts"/> would take for the same command (same command, directory, program and environment), whatever its outcome.</summary>
    /// <param name="record">The run.</param>
    /// <returns>True when it did.</returns>
    public bool Ran(RunRecord record) =>
        Kind == CheckKind.Test && record.Kind == CheckKind.Test && record.CheckSha256 == Sha256 && SameTestRun(record).IsSuccess;

    private Result<RunRecord> SameTestRun(RunRecord record) =>
         (record.Command ?? []).SequenceEqual(Command ?? [], StringComparer.Ordinal) && record.Directory == Directory
            ? SameProgram(record)
            : BugFixErrors.RunRefused.With(
                $"the test command `{string.Join(' ', record.Command ?? [])}` in '{record.Directory}' differs from the baseline's `{string.Join(' ', Command ?? [])}` in '{Directory}'");

    private Result<RunRecord> SameProgram(RunRecord record)
    {
        if (record.ProgramSha256 != ProgramSha256)
        {
            return BugFixErrors.RunRefused.With($"the test command's program `{(Command ?? ["?"])[0]}` resolved to a different file than at the baseline (check PATH)");
        }

        var differ = Environment.Keys.Union(record.Environment.Keys, StringComparer.Ordinal)
            .Where(name => Environment.GetValueOrDefault(name) != record.Environment.GetValueOrDefault(name))
            .Order(StringComparer.Ordinal)
            .ToList();
        return differ.Count == 0
            ? record
            : BugFixErrors.RunRefused.With($"the test command ran with a different {string.Join(", ", differ)} than the baseline; run it with the baseline's values (state.json check.environment has their digests)");
    }
}
