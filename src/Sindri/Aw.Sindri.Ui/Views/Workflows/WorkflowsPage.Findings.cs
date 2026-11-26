using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: ShellWindow.axaml places the page in a Deferred template, which the analyzer cannot see
/// <summary>The Workflows page (<see cref="WorkflowsPage"/>, continued): the failed-check line's Retry.</summary>
internal sealed partial class WorkflowsPage
{
#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnRetryFindings(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.RetryFindings().ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
#pragma warning restore MA0182
