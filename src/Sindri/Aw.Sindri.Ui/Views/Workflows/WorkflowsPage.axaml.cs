using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Workflows page: a title click on a workflow opens its runs, one on a run opens its detail, Back goes up a level.</summary>
internal sealed partial class WorkflowsPage : UserControl
{
    public WorkflowsPage()
    {
        InitializeComponent();
    }

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnOpenWorkflow(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page && sender is Control { DataContext: WorkflowRowViewModel row })
        {
            await page.OpenWorkflow(row.Slug, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnOpenRun(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page && sender is Control { DataContext: RunRowViewModel row })
        {
            await page.OpenRun(row.Id, CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnRetry(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.Retry(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155

    private void OnBack(object? sender, RoutedEventArgs e) => (DataContext as WorkflowsPageViewModel)?.Back();
}
#pragma warning restore MA0182
