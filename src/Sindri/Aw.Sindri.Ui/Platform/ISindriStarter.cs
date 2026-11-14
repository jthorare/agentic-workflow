using Aw.Kernel;

namespace Aw.Sindri.Ui.Platform;

/// <summary>Port: starts the 'sindri' program installed with the app (first-run spec decisions 2 and 2a). Only the desktop head knows how; the views never see a process.</summary>
public interface ISindriStarter
{
    /// <summary>Starts Sindri, unless this run of the app already did.</summary>
    /// <returns>What happened, or why the program didn't start.</returns>
    public Result<SindriStartOutcome> Start();

    /// <summary>Ends the start in flight (serve answered, the wait ran out, or the start failed): the next <see cref="Start"/> launches again. A second call while a start is in flight returns AlreadyStarted.</summary>
    public void Release();

    /// <summary>The last error line serve wrote to its log since the start, or null when it wrote none.</summary>
    /// <returns>The line, or null.</returns>
    public string? LastError();

    /// <summary>Raised when a program this starter started has ended (#3630): its launch number (see <see cref="Launch"/>) and exit code. The app then says why at once instead of waiting out the 30 s.</summary>
    public event EventHandler<SindriExitedEventArgs>? Exited;

    /// <summary>The number of the last launch (0 before the first); an exit of an older number is not the current start's.</summary>
    public int Launch { get; }

    /// <summary>The log the started program writes to.</summary>
    public string LogPath { get; }
}
