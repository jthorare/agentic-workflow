using System.ComponentModel;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell's reaction to the health view's changes (<see cref="ShellViewModel"/>, continued).</summary>
internal sealed partial class ShellViewModel
{
    private void OnHealthChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HealthViewModel.State):
                Changed(_stateProperties);
                Reattend();
                Announce();
                break;
            case nameof(HealthViewModel.Reason):
                OnChanged(e.PropertyName);
                Announce();
                break;
            case nameof(HealthViewModel.ReasonDetail):
                OnChanged(e.PropertyName);
                break;
            default:
                break;
        }
    }
}
