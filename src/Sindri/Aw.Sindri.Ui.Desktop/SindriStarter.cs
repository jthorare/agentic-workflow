using Aw.Kernel;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>Starts the 'sindri' binary beside the app, once per app run (first-run spec §4.1, decisions 2 and 2a). The views never see a process.</summary>
/// <param name="starter">Starts the program.</param>
/// <param name="fileExists">Says whether a file is on disk.</param>
/// <param name="appDirectory">The folder the app runs from.</param>
/// <param name="home">Sindri's state folder; the log is logs/serve.log in it.</param>
internal sealed class SindriStarter(IProcessStarter starter, Func<string, bool> fileExists, string appDirectory, string home)
{
    internal IProcessStarter Starter { get; } = starter;

    internal Func<string, bool> FileExists { get; } = fileExists;

    internal string AppDirectory { get; } = appDirectory;

    internal string Home { get; } = home;

    /// <summary>Starts 'sindri --headless' from beside the app, unless this run already did.</summary>
    /// <returns>What happened, or why the program didn't start.</returns>
    public Result<StartOutcome> Start() => throw new NotSupportedException();
}
