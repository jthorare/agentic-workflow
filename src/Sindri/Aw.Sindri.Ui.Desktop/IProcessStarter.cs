using Aw.Kernel;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>Starts a program detached from the app (a port, so the launch logic is tested without a process).</summary>
internal interface IProcessStarter
{
    /// <summary>Raised when a started program has ended, on any thread: the launch's <see cref="ProcessLaunch.Number"/> and the exit code.</summary>
    public event EventHandler<SindriExitedEventArgs>? Exited;

    /// <summary>Starts <paramref name="launch"/>.</summary>
    /// <param name="launch">The program, its arguments and its log.</param>
    /// <returns>Success once the program is running, or why it didn't start.</returns>
    public Result<Unit> Start(ProcessLaunch launch);
}
