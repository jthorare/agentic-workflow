using Avalonia.Controls;
using Avalonia.Input;
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

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is WorkflowsPageViewModel { IsCompare: true } page)
        {
            page.Back();   // Compare goes back to the runs with its two ticks kept
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnClearRunFilters(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            page.Runs.ClearFilters();
        }
    }

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
}
