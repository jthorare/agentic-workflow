using System.Globalization;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Agents counts in the shell (<see cref="ShellViewModel"/>, continued; U11a T5): the tab's count and the tray's line follow the status' session counts.</summary>
internal sealed partial class ShellViewModel
{
    // Every status carries the counts, whatever page is visible; the tab shows the live figure, the tray adds its line.
    partial void InitAgentCounts()
    {
        // #3710: not running means no figure; the tab would say "Agents 25" under the Not running card.
        // Coming back refills it: Health.Sessions skips an equal value, so OnSessionsChanged alone never fires again.
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(State), StringComparison.Ordinal))
            {
                ShowTabCount(IsNotRunning ? "" : LiveText());
            }
        };
        Health.PropertyChanged += OnSessionsChanged;
    }

    private void OnSessionsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(HealthViewModel.Sessions), StringComparison.Ordinal))
        {
            ShowTabCount(LiveText());
            if (Health.Sessions is { } counts)
            {
                Agents.ObserveCounts(counts.Live, counts.Asking);
            }

            Reattend();
        }
    }

    private string LiveText() => Health.Sessions is { } sessions ? sessions.Live.ToString(CultureInfo.InvariantCulture) : "";

    private void ShowTabCount(string live)
    {
        Agents.ShowNavCount(live);
        Navigation.First(n => n.Page == PageId.Agents).Count = live;
    }
}
