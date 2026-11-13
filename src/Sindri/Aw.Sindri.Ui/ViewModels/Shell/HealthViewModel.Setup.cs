namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Health page's profile card link to Setup (<see cref="HealthViewModel"/>, continued): on a home with no profile, its line opens Setup.</summary>
internal sealed partial class HealthViewModel
{
    /// <summary>The "No profile yet" line was pressed; the shell opens Setup.</summary>
    internal event EventHandler? SetupRequested;

    /// <summary>Asks the shell to open Setup.</summary>
    internal void RequestSetup() => SetupRequested?.Invoke(this, EventArgs.Empty);
}
