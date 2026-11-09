using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Resources;

namespace Aw.Sindri.Ui.Views.Resources;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Resources page: Purge asks first, and the confirm sends one call through its row's view model.</summary>
internal sealed partial class ResourcesPage : UserControl
{
    public ResourcesPage()
    {
        InitializeComponent();
    }

    private static ResourceRowViewModel? Row(object? sender) => (sender as StyledElement)?.DataContext as ResourceRowViewModel;

    private void OnPurge(object? sender, RoutedEventArgs e) => Row(sender)?.AskPurge();

    private void OnKeep(object? sender, RoutedEventArgs e) => Row(sender)?.Keep();

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnRetry(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ResourcesPageViewModel page)
        {
            await page.Retry(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (Row(sender) is { } row)
        {
            await row.ConfirmPurge(CancellationToken.None).ConfigureAwait(true);
        }
    }
}
