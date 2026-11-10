using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Health page: binds to the health view model, holds no logic beyond handing it the page's width, the tab keys and a row's View job press.</summary>
internal sealed partial class HealthPage : UserControl
{
    public HealthPage()
    {
        InitializeComponent();
        SizeChanged += (_, e) => (DataContext as HealthViewModel)?.Resize(e.NewSize.Width);
        SystemCards.SizeChanged += (_, e) => FillCards(e.NewSize.Width);
        SystemTabs.AddHandler(KeyDownEvent, OnTabKey);
    }

    // The card grid fills its width: as many columns as fit the minimum card width, each card an equal share (its own margin inside the share).
    private const double MinCardWidth = 400;

    private void FillCards(double width)
    {
        if (width <= 0)
        {
            return;
        }

        var columns = Math.Max(1, (int)Math.Floor(width / MinCardWidth));
        SystemCards.ItemWidth = Math.Floor(width / columns);
    }

    // Left and Right move between the two tabs, and focus follows the choice (system spec §5).
    private void OnTabKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Left or Key.Right) || DataContext is not HealthViewModel page)
        {
            return;
        }

        if (page.MoveTab(e.Key == Key.Right ? 1 : -1))
        {
            _ = (page.ShowServicesTab ? ServicesTab : OverviewTab).Focus();
        }

        e.Handled = true;
    }

    private void OnViewJob(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ServiceRowViewModel row })
        {
            row.RequestJob();
        }
    }
}
#pragma warning restore MA0182
