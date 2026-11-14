namespace Aw.Sindri.Ui.Platform;

/// <summary>The adapters that belong to the window and so reach the OS through it: where a link or a file opens, and where a file is chosen. One value, so the shell's constructor takes them as one port.</summary>
/// <param name="Launcher">Opens links and files in the system's apps.</param>
/// <param name="Files">Chooses a file for a start input.</param>
/// <param name="Folders">Chooses a folder for the repo step.</param>
/// <param name="Terminal">Opens the terminal WebSocket of a session; null for a head with none (the pane then shows Reconnecting).</param>
/// <param name="Stopper">The stop route; null for a head with none (Stop then answers "Couldn't stop it.").</param>
internal sealed record WindowPorts(ILauncher Launcher, IFilePicker Files, IFolderPicker Folders, Aw.Sindri.Ui.Terminal.ITerminalConnector? Terminal = null, Aw.Sindri.Ui.Terminal.ISessionStopper? Stopper = null)
{
    /// <summary>A head with none: nothing opens and no file or folder is ever chosen.</summary>
    internal static WindowPorts None { get; } = new(NullLauncher.Instance, NullFilePicker.Instance, NullFolderPicker.Instance);
}
