using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Workflows page, continued (#3702): Try again on a run that did not start, from its row or from its run page.</summary>
internal sealed partial class WorkflowsPage
{
#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnRetryStart(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page && sender is Control { DataContext: RunRowViewModel row })
        {
            await page.RetryStart(row.Id, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnRetryStartRunPage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.RetryStart(page.Run.Id, CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
#pragma warning restore MA0182
