namespace Aw.BugFix.Core.Model;

/// <summary>How a test command ended, which program it ran, and in which environment.</summary>
/// <param name="Code">Its exit code (meaningful only when <paramref name="Ran"/>).</param>
/// <param name="Ran">False when the command couldn't start, was killed at the time limit or by a signal, or left an empty log: the check didn't run to a verdict.</param>
/// <param name="ProgramSha256">The SHA-256 of the program file that ran (argv[0] resolved once and started as that file); empty when it didn't resolve.</param>
/// <param name="Environment">The variables that decide which code runs (PATH, HOME, and any the user named), as the command ran with them, each as a keyed digest of its value (never the value: it may hold a credential).</param>
public sealed record CheckExit(int Code, bool Ran, string ProgramSha256, IReadOnlyDictionary<string, string> Environment)
{
    /// <summary>Gets the run's outcome: broken when it didn't run to a verdict, passed on exit 0, failed otherwise.</summary>
    public RunOutcome Outcome => !Ran ? RunOutcome.Broken : Code == 0 ? RunOutcome.Passed : RunOutcome.Failed;
}
