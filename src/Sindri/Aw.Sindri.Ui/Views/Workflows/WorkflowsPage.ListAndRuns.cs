using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Workflows page, continued (#3683): the list's columns follow its width, the runs table's filters and Compare, and Edit step on a failed step.</summary>
internal sealed partial class WorkflowsPage
{
    // The columns that fit: Next run drops first, then the strip shortens and Trigger drops.
    private void WatchListWidth()
    {
        WorkflowsListLevel.SizeChanged += (_, e) =>
        {
            if (DataContext is WorkflowsPageViewModel page)
            {
                page.SetListWidth(e.NewSize.Width);
            }
        };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is WorkflowsPageViewModel page && WorkflowsListLevel.Bounds.Width > 0)
            {
                page.SetListWidth(WorkflowsListLevel.Bounds.Width);
            }
        };
    }

    private void OnClearRunFilters(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            page.Runs.ClearFilters();
        }
    }

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnCompare(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.OpenCompare(CancellationToken.None).ConfigureAwait(true);
            FocusLater(WorkflowsBack);   // Esc then goes back to the runs
        }
    }

    private async void OnEditFailedStep(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: NodeRunViewModel node })
        {
            await node.EditStep(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
