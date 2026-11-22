using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The Workflows page's Delete confirm (<see cref="WorkflowsPage"/>, continued; Workflows page spec §4.9): the ⋯ menu item, the confirm's buttons and its keys.</summary>
internal sealed partial class WorkflowsPage
{
#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    // the ⋯ menu's Delete: opens the confirm, focus on Cancel so a stray Enter deletes nothing
    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            page.RequestDelete();
            Dispatcher.UIThread.Post(() => _ = WorkflowDeleteCancel.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        }
    }

    private void OnDeleteCancel(object? sender, RoutedEventArgs e) => CloseDeleteConfirm();

    private async void OnDeleteYes(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.ConfirmDelete(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnDeletePause(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.PauseFromDelete(CancellationToken.None).ConfigureAwait(true);
            _ = WorkflowDeleteCancel.Focus(NavigationMethod.Tab);   // the off form shows in place; focus starts on Cancel again
        }
    }

    private async void OnDeleteOpenRun(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.OpenDeleteRun(CancellationToken.None).ConfigureAwait(true);
        }
    }

    // Esc closes the confirm; Ctrl+Enter presses Delete in the off form (the footer says both)
    private async void OnDeleteConfirmKey(object? sender, KeyEventArgs e)
    {
        if (DataContext is not WorkflowsPageViewModel page)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseDeleteConfirm();
        }
        else if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control) && page.CanConfirmDelete)
        {
            e.Handled = true;
            await page.ConfirmDelete(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private void CloseDeleteConfirm()
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            page.CancelDelete();
            _ = WorkflowMore.Focus(NavigationMethod.Tab);
        }
    }
#pragma warning restore MA0155
}
