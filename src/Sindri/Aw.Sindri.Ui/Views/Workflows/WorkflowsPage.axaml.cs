using Avalonia;
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

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;   // the title's text, its scale or the crumb's text changed: the crumb follows the title's baseline
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (WorkflowsBack.IsVisible)
        {
            AlignCrumb(WorkflowsBack, WorkflowsBack.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault());
            AlignCrumb(WorkflowsCrumbSep, WorkflowsCrumbSep);
        }
    }

    // Moves a top-aligned crumb part down by the gap between its text's baseline and the page title's (#2807).
    // Under half a pixel is left alone, so the layout it causes settles on the next pass.
    private void AlignCrumb(Control part, TextBlock? text)
    {
        if (text is null || text.TranslatePoint(default, this) is not { } at || WorkflowsTitle.TranslatePoint(default, this) is not { } title)
        {
            return;
        }

        var gap = title.Y + WorkflowsTitle.TextLayout.Baseline - (at.Y + text.TextLayout.Baseline);
        if (Math.Abs(gap) >= 0.5)
        {
            var m = part.Margin;
            part.Margin = new Thickness(m.Left, m.Top + gap, m.Right, m.Bottom);
        }
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

    private void OnEdit(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            page.Edit();
            FocusLater(WorkflowEditDone);   // Edit hides itself in edit mode: focus moves to its neighbour, Done, not the breadcrumb
        }
    }

    private void OnDone(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            page.CloseEditor();
            FocusEdit();
        }
    }

    private async void OnStepSettings(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.OpenSettings(CancellationToken.None).ConfigureAwait(true);   // the fields exist only once the form is read: focus the first one after
            Dispatcher.UIThread.Post(() => _ = (FirstTarget(WorkflowEditor, static c => c is TextBox or CheckBox or Button) ?? WorkflowEditor).Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        }
    }

    private void OnNewWorkflow(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            page.OpenCreate();
            FocusLater(WorkflowsNewName);   // the form opens on its name field
        }
    }

    private async void OnCreate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            var create = page.Create(CancellationToken.None);
            await create.ConfigureAwait(true);
            if (page.IsRuns)
            {
                FocusLater(WorkflowsBack);   // the new workflow's page opens on its Back button, as any workflow does
            }
        }
    }

    private void OnCancelCreate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            page.CancelCreate();
            FocusLater(WorkflowsNewHeader);
        }
    }

    private void OnCreateKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is WorkflowsPageViewModel { CanCreate: true })
            {
                OnCreate(sender, e);
                e.Handled = true;   // only a submit swallows Enter
            }
        }
        else if (e.Key == Key.Escape)
        {
            OnCancelCreate(sender, e);
            e.Handled = true;
        }
    }

    // The row is the focus stop: Enter or Space on it (not on a control inside it) opens the workflow, as clicking its title does.
    private async void OnWorkflowRowKey(object? sender, KeyEventArgs e)
    {
        if (e.Source == sender && e.Key is Key.Enter or Key.Space && DataContext is WorkflowsPageViewModel page && sender is Control { DataContext: WorkflowRowViewModel row })
        {
            e.Handled = true;
            var open = page.OpenWorkflow(row.Slug, CancellationToken.None);
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

    private async void OnActivate(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.Activate(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnPause(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.Pause(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnTakeOverNode(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: NodeRunViewModel node })
        {
            await node.TakeOver(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnEditFailedStep(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: NodeRunViewModel node })
        {
            await node.EditStep(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnHandBackNode(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: NodeRunViewModel node })
        {
            await node.HandBack(CancellationToken.None).ConfigureAwait(true);
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

    // Back lands on the level it returns to: the first row's focus stop (the workflow row itself, or a run row's button), else the first thing on that level that takes focus (Try again, Run now), never the Back button it just hid.
    private void OnBack(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkflowsPageViewModel page)
        {
            return;
        }

        page.Back();
        var rows = page.IsList ? WorkflowRows : RunRows;
        Control level = page.IsList ? WorkflowsListLevel : WorkflowRunsLevel;
        Func<Control, bool> stop = page.IsList ? static c => c is Border b && b.Classes.Contains("item-row") : static c => c is Button;   // a workflow row is its own focus stop (#2550); a run row's title button is still one
        Dispatcher.UIThread.Post(
            () => _ = (FirstTarget(rows, stop) ?? FirstTarget(level, static c => c.Focusable) ?? level).Focus(NavigationMethod.Tab),
            DispatcherPriority.Loaded);
    }

    /// <summary>Returns focus to the Edit button (Done), or to Step settings while edit mode stays on (the sheet was closed).</summary>
    internal void FocusEdit() =>
        Dispatcher.UIThread.Post(() => _ = (WorkflowEdit.IsEffectivelyVisible ? WorkflowEdit : StepSettings).Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);   // Edit is hidden while edit mode is on: Step settings is the neighbour then

    private static Control? FirstTarget(Control within, Func<Control, bool> wanted) =>
        within.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => wanted(c) && c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled);

    private static void FocusLater(Control control) => Dispatcher.UIThread.Post(() => _ = control.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
}
#pragma warning restore MA0182
