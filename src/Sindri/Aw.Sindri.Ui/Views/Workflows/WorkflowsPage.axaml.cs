using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Workflows page: a title click on a workflow opens its runs, one on a run opens its detail, Back goes up a level. Opening or leaving a level moves focus into it.</summary>
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
            var open = page.OpenWorkflow(row.Slug, CancellationToken.None);
            FocusLater(WorkflowsBack);   // a level opens on its Back button
            await open.ConfigureAwait(true);
        }
    }

    private async void OnOpenRun(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page && sender is Control { DataContext: RunRowViewModel row })
        {
            var open = page.OpenRun(row.Id, CancellationToken.None);
            FocusLater(WorkflowsBack);
            await open.ConfigureAwait(true);
        }
    }

    private async void OnRetry(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.Retry(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnRetryRuns(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.RetryRuns(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnRetryRun(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.RetryRun(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnRunNow(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.RunNow(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnCancelRun(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.CancelRun(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155

    // Back lands on the level it returns to: the first row of the list or of the runs, else the Back button when that level has no row.
    private void OnBack(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkflowsPageViewModel page)
        {
            return;
        }

        page.Back();
        var rows = page.IsList ? WorkflowRows : RunRows;
        Dispatcher.UIThread.Post(() => _ = (rows.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsEffectivelyVisible) ?? (Control)WorkflowsBack).Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }

    private static void FocusLater(Control control) => Dispatcher.UIThread.Post(() => _ = control.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
}
#pragma warning restore MA0182
