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

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Home's panel frames and its health strip, which passes its click to the shell.</summary>
internal sealed partial class HomePage : UserControl
{
    // The focus ring is drawn outside its control (Margin -4 in Theme/FluentOverrides.axaml: 4 px past the control, the layouts' 4 px of room). Scrolling a focused control into
    // view uses its own bounds, so on a page taller than the window the ring landed past the viewport's edge and was cut off. Home grew taller with its count row (#2780),
    // so it asks the scroller above for the control plus that room.
    private const double FocusRingReach = 4;

    private static void OnRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e) =>
        e.TargetRect = e.TargetRect.Inflate(FocusRingReach);

    public HomePage()
    {
        InitializeComponent();
        AddHandler(ItemRow.OpenEvent, OnOpen);
        AddHandler(ItemRow.SelectEvent, OnSelect);
        AddHandler(NeedsYouRow.OpenEvent, OnNeedsYouOpen);
        AddHandler(NeedsYouRow.SelectEvent, OnNeedsYouSelect);
        AddHandler(NeedsYouRow.ActionEvent, OnNeedsYouAction);
        AddHandler(RequestBringIntoViewEvent, OnRequestBringIntoView);
        AddHandler(GotFocusEvent, (_, e) => Look(e, looking: true), handledEventsToo: true);
        AddHandler(LostFocusEvent, (_, e) => Look(e, looking: false), handledEventsToo: true);
        AddHandler(GotFocusEvent, (_, e) => Region(e, on: true), handledEventsToo: true);
        AddHandler(LostFocusEvent, (_, e) => Region(e, on: false), handledEventsToo: true);
        DataContextChanged += (_, _) => FollowNeedsYouRows();
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

    // Below 1100 px the two columns become one (F3 home spec §2): Needs you over Running over Queued, each the page's full width.
    private void Stack(bool stacked)
    {
        var columns = this.FindControl<Grid>("HomeColumns");
        var right = this.FindControl<StackPanel>("HomeRight");
        if (columns is null || right is null)
        {
            return;
        }

        columns.ColumnDefinitions = stacked ? [new ColumnDefinition(GridLength.Star)] : [new ColumnDefinition(3, GridUnitType.Star), new ColumnDefinition(16, GridUnitType.Pixel), new ColumnDefinition(2, GridUnitType.Star)];
        columns.RowDefinitions = stacked ? [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)] : [];
        Grid.SetColumn(right, stacked ? 0 : 2);
        Grid.SetRow(right, stacked ? 1 : 0);
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

    private void OnRetryNeedsYou(object? sender, RoutedEventArgs e) => _ = (DataContext as ShellViewModel)?.RetryNeedsYou();

    private void OnUndoSeen(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.NeedsYou.Seen.Undo();

    private void OnShowAllNeedsYou(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.OpenNeedsYouInbox();

    private void OnOpenReviews(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.OpenReviews();

    private void OnOpenLinear(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.OpenLinear();

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

    private void OnSetupLink(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Setup.RequestChecklist();

    private void OnSetupContinue(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Setup.Continue();

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
