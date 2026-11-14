using System.ComponentModel;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell's reaction to the health view's changes (<see cref="ShellViewModel"/>, continued).</summary>
internal sealed partial class ShellViewModel
{
    // The start card's stage drives the pill's Starting word (#3672).
    private void OnStartChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SindriStartViewModel.Stage), StringComparison.Ordinal))
        {
            Health.SetStarting(Start.IsStarting);
        }
    }

    private void OnHealthChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HealthViewModel.State):
                Changed(_stateProperties);
                Start.Observe(State);

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
