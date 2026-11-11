namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The Projects page (<see cref="ProjectsPageViewModel"/>, continued): Connect Linear, the second way in when Linear is not set up or Sindri has no key (#2549).</summary>
internal sealed partial class ProjectsPageViewModel
{
    /// <summary>Connect Linear was pressed: the shell opens Connections.</summary>
    internal event EventHandler? ConnectRequested;

    /// <summary>Asks the shell to open Connections.</summary>
    internal void RequestConnect() => ConnectRequested?.Invoke(this, EventArgs.Empty);
}
