namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>How a short-lived helper process ended.</summary>
/// <param name="Started">False when the program could not be started (it is not there).</param>
/// <param name="ExitCode">The exit code, or -1 when it did not exit.</param>
/// <param name="Output">What it printed on standard output.</param>
/// <param name="Error">What it printed on standard error.</param>
/// <param name="TimedOut">True when it was killed for taking too long.</param>
internal sealed record ProcessResult(bool Started, int ExitCode, string Output, string Error, bool TimedOut)
{
    /// <summary>Gets a result for a program that could not be started.</summary>
    internal static ProcessResult NotStarted { get; } = new(Started: false, ExitCode: -1, Output: string.Empty, Error: string.Empty, TimedOut: false);

    /// <summary>Gets a value indicating whether the program started and exited with 0.</summary>
    internal bool Succeeded => Started && !TimedOut && ExitCode == 0;
}
