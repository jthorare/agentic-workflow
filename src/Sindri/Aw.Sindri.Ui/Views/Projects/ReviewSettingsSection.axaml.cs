using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Views.Projects;

#pragma warning disable MA0182 // why: ProjectSheetView.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The project sheet's Review settings: its buttons call the view model; each row's button finds its row through its DataContext.</summary>
internal sealed partial class ReviewSettingsSection : UserControl
{
    public ReviewSettingsSection()
    {
        InitializeComponent();
    }

    private ReviewSettingsViewModel? Settings => DataContext as ReviewSettingsViewModel;

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ReviewSettingsKey key })
        {
            Settings?.Reset(key);
        }
    }

    private void OnPanelUp(object? sender, RoutedEventArgs e) => MovePanel(sender, -1);

    private void OnPanelDown(object? sender, RoutedEventArgs e) => MovePanel(sender, 1);

    private void MovePanel(object? sender, int by)
    {
        if (sender is Control { DataContext: SettingsPanelRow row })
        {
            Settings?.MovePanel(row, by);
        }
    }

    private void OnAddGroup(object? sender, RoutedEventArgs e) => Settings?.AddGroup();

    private void OnRemoveGroup(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SettingsGroupRow row })
        {
            Settings?.RemoveGroup(row);
        }
    }

    private void OnAddRisk(object? sender, RoutedEventArgs e) => Settings?.AddRisk();

    private void OnRemoveRisk(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SettingsRiskRow row })
        {
            Settings?.RemoveRisk(row);
        }
    }

    private void OnAddCheck(object? sender, RoutedEventArgs e) => Settings?.AddCheck();

    private void OnRemoveCheck(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: SettingsCheckRow row })
        {
            Settings?.RemoveCheck(row);
        }
    }

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (Settings is { } settings)
        {
            await settings.Save(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnDiscard(object? sender, RoutedEventArgs e)
    {
        if (Settings is { } settings)
        {
            await settings.Discard(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
#pragma warning restore MA0182
