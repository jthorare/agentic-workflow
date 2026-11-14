using Aw.Kernel;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>Starts the 'sindri' binary beside the app, once per app run (first-run spec §4.1, decisions 2 and 2a). The views never see a process.</summary>
/// <param name="starter">Starts the program.</param>
/// <param name="fileExists">Says whether a file is on disk.</param>
/// <param name="appDirectory">The folder the app runs from.</param>
/// <param name="home">Sindri's state folder; the log is logs/serve.log in it.</param>
/// <param name="installedBinary">The installed 'sindri' ($AW_STATE_DIR/bin/cs/current/sindri), preferred over the one beside the app when it exists (decision 2).</param>
internal sealed class SindriStarter(IProcessStarter starter, Func<string, bool> fileExists, string appDirectory, string home, string? installedBinary = null)
{
    internal IProcessStarter Starter { get; } = starter;

    internal Func<string, bool> FileExists { get; } = fileExists;

    internal string AppDirectory { get; } = appDirectory;

    internal string Home { get; } = home;

    private bool _started;

    /// <summary>Ends the start in flight (serve answered, the wait ran out, or the start failed), so the next <see cref="Start"/> launches again.</summary>
    public void Release() => _started = false;

    /// <summary>The number of the last launch (0 before the first).</summary>
    public int Launch { get; private set; }

    /// <summary>Starts 'sindri --headless' from beside the app, unless this run already did.</summary>
    /// <returns>What happened, or why the program didn't start.</returns>
    public Result<StartOutcome> Start()
    {
        if (_started)
        {
            return StartOutcome.AlreadyStarted;
        }

        var beside = Path.Combine(AppDirectory, "sindri");
        var executable = installedBinary is { Length: > 0 } && FileExists(installedBinary) ? installedBinary
            : FileExists(beside) ? beside
            : null;
        if (executable is null)
        {
            return StartOutcome.NotInstalled;
        }

        var started = Starter.Start(new ProcessLaunch(executable, ["--headless"], Path.Combine(Home, "logs", "serve.log"), ++Launch));
        if (started.IsError)
        {
            return started.Error;
        }

        _started = true;
        return StartOutcome.Started;
    }
}
