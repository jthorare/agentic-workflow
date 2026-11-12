using Aw.Kernel;

namespace Aw.Sindri.Ui.Platform;

/// <summary>Port: starts the 'sindri' program installed with the app (first-run spec decisions 2 and 2a). Only the desktop head knows how; the views never see a process.</summary>
public interface ISindriStarter
{
    /// <summary>Starts Sindri, unless this run of the app already did.</summary>
    /// <returns>What happened, or why the program didn't start.</returns>
    public Result<SindriStartOutcome> Start();

    /// <summary>The last error line serve wrote to its log since the start, or null when it wrote none.</summary>
    /// <returns>The line, or null.</returns>
    public string? LastError();

    /// <summary>The log the started program writes to.</summary>
    public string LogPath { get; }
}
