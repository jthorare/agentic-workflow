namespace Aw.Sindri.Ui.Platform;

/// <summary>A program the app started has ended (#3630).</summary>
/// <param name="launch">The launch's number (see <see cref="ISindriStarter.Launch"/>).</param>
/// <param name="exitCode">The program's exit code.</param>
public sealed class SindriExitedEventArgs(int launch, int exitCode) : EventArgs
{
    /// <summary>The launch's number.</summary>
    public int Launch { get; } = launch;

    /// <summary>The program's exit code.</summary>
    public int ExitCode { get; } = exitCode;
}
