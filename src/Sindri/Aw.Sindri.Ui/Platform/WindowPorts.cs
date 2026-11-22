namespace Aw.Sindri.Ui.Platform;

/// <summary>The adapters that belong to the window and so reach the OS through it: where a link or a file opens, and where a file is chosen. One value, so the shell's constructor takes them as one port.</summary>
/// <param name="Launcher">Opens links and files in the system's apps.</param>
/// <param name="Files">Chooses a file for a start input.</param>
/// <param name="Folders">Chooses a folder for the repo step.</param>
internal sealed record WindowPorts(ILauncher Launcher, IFilePicker Files, IFolderPicker Folders)
{
    /// <summary>Starts the installed 'sindri' when the app finds it down; none when the head has no way.</summary>
    internal ISindriStarter? Starter { get; init; }

    /// <summary>The app's log (the start states write debug lines to it); none by default.</summary>
    internal Microsoft.Extensions.Logging.ILogger Log { get; init; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    /// <summary>A head with none: nothing opens and no file or folder is ever chosen.</summary>
    internal static WindowPorts None { get; } = new(NullLauncher.Instance, NullFilePicker.Instance, NullFolderPicker.Instance);
}
