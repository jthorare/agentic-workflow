using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Layout;
using Aw.Sindri.Ui.Views.NeedsYou;
using Aw.Sindri.Ui.Views.Queue;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Home's panel frames and its health strip, which passes its click to the shell.</summary>
internal sealed partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
        AddHandler(ItemRow.OpenEvent, OnOpen);
        AddHandler(ItemRow.SelectEvent, OnSelect);
        AddHandler(NeedsYouRow.OpenEvent, OnNeedsYouOpen);
        AddHandler(NeedsYouRow.SelectEvent, OnNeedsYouSelect);
        AddHandler(NeedsYouRow.ActionEvent, OnNeedsYouAction);
        AddHandler(GotFocusEvent, (_, e) => Look(e, looking: true), handledEventsToo: true);
        AddHandler(LostFocusEvent, (_, e) => Look(e, looking: false), handledEventsToo: true);
        AddHandler(StripRow.OpenEvent, OnStripOpen);
        AddHandler(StripRow.SelectEvent, OnStripSelect);
        AddHandler(StripRow.AnswerEvent, OnStripAnswer);
        AddHandler(ChildAnswerButton.FollowEvent, OnChildAnswer);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageLayout.TierProperty)
        {
            Stack(change.GetNewValue<PageTier>() >= PageTier.Narrow);
        }
    }

    // Below 1000 px the two panel columns become one (proof B8): Running over Queued, each the page's full width.
    private void Stack(bool stacked)
    {
        var grid = this.FindControl<Grid>("PanelGrid");
        var queued = this.FindControl<Border>("QueuedPanel");
        if (grid is null || queued is null)
        {
            return;
        }

        grid.ColumnDefinitions = stacked ? [new ColumnDefinition(GridLength.Star)] : [new ColumnDefinition(GridLength.Star), new ColumnDefinition(16, GridUnitType.Pixel), new ColumnDefinition(GridLength.Star)];
        grid.RowDefinitions = stacked ? [new RowDefinition(GridLength.Auto), new RowDefinition(16, GridUnitType.Pixel), new RowDefinition(GridLength.Auto)] : [];
        Grid.SetColumn(queued, stacked ? 0 : 2);
        Grid.SetRow(queued, stacked ? 2 : 0);
    }

    private void OnHealthStrip(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.GoTo(PageId.System);

    // "Show all N in Queue": the Queue page, filtered to the panel's group for Running (G R), whole for Queued.
    private void OnShowAll(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell && sender is Button { Tag: string tag })
        {
            shell.GoToQueue(tag == "Running" ? QueueFilter.Running : QueueFilter.All);
        }
    }

    private void OnMarkAllSeen(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.MarkAllSeen();

    private void OnUndoSeen(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.NeedsYou.Seen.Undo();

    private void OnShowAllNeedsYou(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.OpenNeedsYouInbox();

    private void OnShowNeedsYouHeld(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.NeedsYou.ShowHeld();

    // Focus arriving on a Needs-you row selects it, and the Queue's selection gives way: one selection on Home.
    private void OnNeedsYouSelect(object? sender, RoutedEventArgs e)
    {
        if (e.Source is NeedsYouRow { RowId.Length: > 0 } row && DataContext is ShellViewModel shell)
        {
            shell.SelectNeedsYouRow(row.RowId);
        }
    }

    private void OnNeedsYouOpen(object? sender, RoutedEventArgs e)
    {
        if (e.Source is NeedsYouRow { RowId.Length: > 0 } row && DataContext is ShellViewModel shell)
        {
            shell.SelectNeedsYouRow(row.RowId);
            shell.OpenNeedsYouRow(row.RowId);
        }
    }

    private void OnNeedsYouAction(object? sender, RoutedEventArgs e)
    {
        if (NeedsYouRow.ActionOf(e) is { } action && e.Source is NeedsYouRow { RowId.Length: > 0 } row && DataContext is ShellViewModel shell)
        {
            shell.NeedsYou.Press(row.RowId, action);
        }
    }

    private void OnStart(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.OpenStart();

    private void OnWorkflowsMore(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Strip.OpenWorkflows();

    // Focus arriving on a strip row selects it, and the other lists' selections give way: one selection on Home.
    private void OnStripSelect(object? sender, RoutedEventArgs e)
    {
        if (e.Source is StripRow { Address.Length: > 0 } row && DataContext is ShellViewModel shell)
        {
            shell.SelectStripRow(row.Address);
        }
    }

    private void OnStripOpen(object? sender, RoutedEventArgs e)
    {
        if (e.Source is StripRow { Address.Length: > 0 } row && DataContext is ShellViewModel shell)
        {
            shell.SelectStripRow(row.Address);
            shell.OpenStripRow(row.Address);
        }
    }

    // "Answer in the child" on a strip or needs-you row: the row is selected and stays the opener, like its other buttons.
    private void OnChildAnswer(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not ChildAnswerButton { Answer: { } answer } button || DataContext is not ShellViewModel shell)
        {
            return;
        }

        if (button.FindAncestorOfType<StripRow>() is { Address.Length: > 0 } strip)
        {
            shell.FollowChildAnswerFromStrip(strip.Address, answer);
        }
        else if (button.FindAncestorOfType<NeedsYouRow>() is { RowId.Length: > 0 } needsYou)
        {
            shell.FollowChildAnswerFromNeedsYou(needsYou.RowId, answer);
        }
    }

    private void OnStripAnswer(object? sender, RoutedEventArgs e)
    {
        if (e.Source is StripRow { Address.Length: > 0 } row && DataContext is ShellViewModel shell)
        {
            shell.AnswerStripRow(row.Address);
        }
    }

    // A row is seen once it has been on screen and focused, in the active window, for a second (§5): focus in a row starts the
    // count, focus out of it stops it, and so does the window going inactive (it starts again when the window is activated).
    private void Look(RoutedEventArgs e, bool looking)
    {
        if ((e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<NeedsYouRow>().FirstOrDefault() is { } row)
        {
            Watch(row, looking);
        }
    }

    private void Watch(NeedsYouRow row, bool looking, bool? windowActive = null)
    {
        if (row.RowId is { Length: > 0 } id && DataContext is ShellViewModel shell)
        {
            shell.NeedsYou.Seen.Watch(id, looking && row.IsEffectivelyVisible && (windowActive ?? (TopLevel.GetTopLevel(this) is Window { IsActive: true })));
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Activated += OnWindowActivated;
            window.Deactivated += OnWindowDeactivated;
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Activated -= OnWindowActivated;
            window.Deactivated -= OnWindowDeactivated;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnWindowActivated(object? sender, EventArgs e) => WatchFocusedRow(active: true);

    private void OnWindowDeactivated(object? sender, EventArgs e) => WatchFocusedRow(active: false);

    // IsActive is not yet true inside Activated, so the event says which way the window went.
    private void WatchFocusedRow(bool active)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused
            && focused.GetSelfAndVisualAncestors().OfType<NeedsYouRow>().FirstOrDefault() is { } row)
        {
            Watch(row, looking: active, windowActive: active);
        }
    }

    // The pointer over the Needs-you panel holds new rows behind the "N new" control, so none arrives under it (§3.3).
    private void OnNeedsYouPanelEntered(object? sender, Avalonia.Input.PointerEventArgs e) => SetNeedsYouHold(holding: true);

    private void OnNeedsYouPanelExited(object? sender, Avalonia.Input.PointerEventArgs e) => SetNeedsYouHold(holding: false);

    private void SetNeedsYouHold(bool holding)
    {
        if (DataContext is ShellViewModel shell)
        {
            shell.NeedsYou.PointerOver = holding;
        }
    }

    // The Undo offer waits while its button has focus or the pointer (WCAG 2.2.1), and starts over when both are gone.
    private void OnUndoHeld(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.NeedsYou.Seen.HoldUndo(holding: true);

    private void OnUndoReleased(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { IsKeyboardFocusWithin: false, IsPointerOver: false })
        {
            (DataContext as ShellViewModel)?.NeedsYou.Seen.HoldUndo(holding: false);
        }
    }

    private void OnShowHeld(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Queue.ShowHeld(QueueScope.Home);

    private void OnPanelEntered(object? sender, Avalonia.Input.PointerEventArgs e) => (DataContext as ShellViewModel)?.Queue.Hold(holding: true);

    private void OnPanelExited(object? sender, Avalonia.Input.PointerEventArgs e) => (DataContext as ShellViewModel)?.Queue.Hold(holding: false);

    // Focus arriving on a row, or on a control in it, selects it: the selection is the focus (§3.3).
    private void OnSelect(object? sender, RoutedEventArgs e)
    {
        if (ItemRow.ItemIdOf(e) is { Length: > 0 } id && DataContext is ShellViewModel shell)
        {
            shell.SelectQueueRow(id);
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (ItemRow.ItemIdOf(e) is { Length: > 0 } id && DataContext is ShellViewModel shell)
        {
            shell.SelectQueueRow(id);
            shell.OpenItem(id);
        }
    }
}
