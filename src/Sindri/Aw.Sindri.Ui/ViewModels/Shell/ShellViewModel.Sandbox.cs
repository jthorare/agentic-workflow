namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Sandbox card's follow (<see cref="ShellViewModel"/>, continued; sandbox spec 8).</summary>
internal sealed partial class ShellViewModel
{
    // The status is read on connect and after every event with the other views.
    private void InitSandbox() => Health.Follow(Health.Sandbox.Refresh);
}
