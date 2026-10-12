namespace Aw.Judge.Core.Processes;

/// <summary>What a program run produced.</summary>
/// <param name="End">How it ended.</param>
/// <param name="ExitCode">The exit code when it exited by itself; null otherwise.</param>
/// <param name="StandardOutput">Everything it wrote to standard output (what was read before it was stopped, on a timeout).</param>
/// <param name="StandardError">Everything it wrote to standard error.</param>
public sealed record ProcessResult(ProcessEnd End, int? ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>Whether it ran past its timeout.</summary>
    public bool TimedOut => End == ProcessEnd.TimedOut;

    /// <summary>A program that exited by itself.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="standardOutput">Its standard output.</param>
    /// <param name="standardError">Its standard error.</param>
    /// <returns>The result.</returns>
    public static ProcessResult Exited(int exitCode, string standardOutput, string standardError) =>
        new(ProcessEnd.Exited, exitCode, standardOutput, standardError);

    /// <summary>A program stopped at its timeout.</summary>
    /// <param name="standardOutput">What it wrote before it was stopped.</param>
    /// <param name="standardError">What it wrote to standard error before it was stopped.</param>
    /// <returns>The result.</returns>
    public static ProcessResult TimedOutAfter(string standardOutput, string standardError) =>
        new(ProcessEnd.TimedOut, ExitCode: null, standardOutput, standardError);

    /// <summary>A program that couldn't be started.</summary>
    /// <returns>The result.</returns>
    public static ProcessResult NotStarted() => new(ProcessEnd.NotStarted, ExitCode: null, "", "");

    /// <summary>A program the system refused to start.</summary>
    /// <param name="reason">Why, as the system said it (standard error).</param>
    /// <returns>The result.</returns>
    public static ProcessResult FailedToStart(string reason) => new(ProcessEnd.FailedToStart, ExitCode: null, "", reason);
}
