namespace Aw.Sindri.Core.Checks;

/// <summary>How a command ended: its exit code, whether it ran out of time, how long it ran and the tail of what it printed.</summary>
/// <param name="ExitCode">The exit code; -1 when the command was killed for time.</param>
/// <param name="TimedOut">True when the command ran past its timeout and was killed.</param>
/// <param name="Duration">How long it ran.</param>
/// <param name="Tail">The end of its output (standard output, then standard error): at most <see cref="TailLimit"/> characters, with secrets replaced.</param>
public sealed record CheckRun(int ExitCode, bool TimedOut, TimeSpan Duration, string Tail)
{
    /// <summary>The most characters of output a run keeps.</summary>
    public const int TailLimit = 4000;

    /// <summary>True when the command exited 0 in time.</summary>
    public bool Passed => !TimedOut && ExitCode == 0;

    /// <summary>A run whose tail is cut to the last <see cref="TailLimit"/> characters of <paramref name="output"/>.</summary>
    /// <param name="exitCode">The exit code.</param>
    /// <param name="timedOut">Whether it was killed for time.</param>
    /// <param name="duration">How long it ran.</param>
    /// <param name="output">All it printed, already scrubbed.</param>
    /// <returns>The run.</returns>
    public static CheckRun Of(int exitCode, bool timedOut, TimeSpan duration, string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new CheckRun(exitCode, timedOut, duration, output.Length <= TailLimit ? output : output[^TailLimit..]);
    }
}
