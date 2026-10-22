using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.NeedsYou;

/// <summary>
/// The Needs-you inbox page (F3 needs-you spec §2): the grouped list and the request card. A click or focus on a row selects it,
/// a card control is pressed as Home's row control is, and the list column is 330 px wide under Size.SheetCoversBelow.
/// </summary>
internal sealed partial class NeedsYouPage : UserControl
{
    // The page's wide and narrow list columns (spec §2: 400, and 330 under 1100 px).
    private const double WideColumn = 400;
    private const double NarrowColumn = 330;
    private const double NarrowBelow = 1100;

    private NeedsYouPageViewModel? _page;
    private NeedsYouBarMode _mode;

    public NeedsYouPage()
    {
        InitializeComponent();
        AddHandler(NeedsYouInboxRow.SelectEvent, OnRowSelected);
        AddHandler(GotFocusEvent, (_, e) => Look(e, looking: true));
        AddHandler(LostFocusEvent, (_, e) => Look(e, looking: false));
        // Tunnel: the multi-line box handles Enter itself before a bubbling handler would see Ctrl+Enter.
        NeedsYouReason.AddHandler(KeyDownEvent, OnReasonKey, RoutingStrategies.Tunnel);
        NeedsYouHoldControl.Held += (_, e) => _ = _page?.Bar.HoldReached(e.HeldMs);   // the hold that reached the server's time is the person's: the bar sends it once
        NeedsYouTypedBox.Submitted += (_, e) => _ = _page?.Bar.SubmitTyped(e.Text);
        SizeChanged += (_, e) => NeedsYouColumns.ColumnDefinitions[0].Width = new GridLength(e.NewSize.Width < NarrowBelow ? NarrowColumn : WideColumn);
    }

    /// <summary>Focuses the selected row's frame, on the keyboard's ring; false when it is not drawn.</summary>
    /// <param name="rowId">The row's id.</param>
    /// <returns>True when focus moved to the row, or was already in it.</returns>
    internal bool FocusRow(string rowId)
    {
        var row = NeedsYouGroups.GetVisualDescendants().OfType<NeedsYouInboxRow>()
            .FirstOrDefault(r => r.IsEffectivelyVisible && string.Equals(r.RowId, rowId, StringComparison.Ordinal));
        if (row is null)
        {
            return false;
        }

        var frame = row.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "InboxRowFrame", StringComparison.Ordinal));
        var focused = row.IsKeyboardFocusWithin || frame?.Focus(NavigationMethod.Tab) == true;
        if (focused && frame is not null)
        {
            Dispatcher.UIThread.Post(() => StickyGroupList.EnsureVisible(frame), DispatcherPriority.Loaded);   // below the sticky header, not under it
        }

        return focused;
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_page is not null)
        {
            _page.PropertyChanged -= OnPageChanged;
            _page.Bar.ReasonRequested -= OnReasonRequested;
            _page.Bar.ConfirmFocusRequested -= OnConfirmFocusRequested;
            _page.Bar.PropertyChanged -= OnBarChanged;
            _page.CardFocusRequested -= OnCardFocusRequested;
        }

        _page = DataContext as NeedsYouPageViewModel;
        if (_page is not null)
        {
            _page.PropertyChanged += OnPageChanged;
            _page.Bar.ReasonRequested += OnReasonRequested;
            _page.Bar.ConfirmFocusRequested += OnConfirmFocusRequested;
            _page.Bar.PropertyChanged += OnBarChanged;
            _mode = _page.Bar.Mode;
            _page.CardFocusRequested += OnCardFocusRequested;
            NeedsYouDropItem.Header = _page.Bar.DropWord;   // a flyout's items do not take the page's data context
        }
    }

    // Another request on the card starts it at the top, and what "read to the end" means is read again once it is drawn.
    private void OnPageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(NeedsYouPageViewModel.Card), StringComparison.Ordinal))
        {
            NeedsYouCardScroll.ScrollToHome();
            Dispatcher.UIThread.Post(UpdateRead, DispatcherPriority.Loaded);
        }
    }

    private void OnCardScrolled(object? sender, ScrollChangedEventArgs e) => UpdateRead();

    // §5 open item 4: "read" is the scroll flag only, and a card shorter than its viewport is read at once. A card that is not drawn says nothing.
    private void UpdateRead()
    {
        var scroll = NeedsYouCardScroll;
        if (_page is not null && scroll.IsEffectivelyVisible && scroll.Viewport.Height > 0)
        {
            _page.Bar.ReadToEnd = scroll.Extent.Height <= scroll.Viewport.Height + 1 || scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - 1;
        }
    }

    // The reason box opened (D): focus goes into it, so typing starts there.
    private void OnReasonRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = NeedsYouReason.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);

    // A: focus moves to the confirmation control (the typed box's field, or the hold button), so Enter types or Space holds there. Nothing is approved by it.
    private void OnConfirmFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_page?.Bar.ShowsTyped == true)
            {
                NeedsYouTypedBox.FocusField();
            }
            else if (_page?.Bar.ShowsHold == true)
            {
                _ = NeedsYouHoldControl.Part.Focus(NavigationMethod.Tab);
            }
        }, DispatcherPriority.Loaded);

    // What closes under the keyboard takes its focus with it: a closed question hands focus back to Send back (or Approve), and a report that went through hands it to the
    // list row, so focus never falls to the window.
    private void OnBarChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_page is null || !string.Equals(e.PropertyName, nameof(NeedsYouBarViewModel.Mode), StringComparison.Ordinal) || _page.Bar.Mode == _mode)
        {
            return;
        }

        var (was, now) = (_mode, _page.Bar.Mode);
        _mode = now;
        if (now == NeedsYouBarMode.Approved)
        {
            Dispatcher.UIThread.Post(() => _ = _page?.Selected is { } row && FocusRow(row.Id), DispatcherPriority.Loaded);
        }
        else if (now == NeedsYouBarMode.Pending && was is NeedsYouBarMode.SendingBack or NeedsYouBarMode.ConfirmingDrop or NeedsYouBarMode.Confirming)
        {
            Dispatcher.UIThread.Post(() => FocusBar(was), DispatcherPriority.Loaded);
        }
    }

    private void FocusBar(NeedsYouBarMode was)
    {
        Button[] order = was == NeedsYouBarMode.Confirming ? [NeedsYouApprove, NeedsYouSendBack] : [NeedsYouSendBack, NeedsYouApprove, NeedsYouTerminal];
        _ = order.FirstOrDefault(b => b.IsEffectivelyVisible && b.IsEnabled)?.Focus(NavigationMethod.Tab);
    }

    // Enter on the list: focus goes to the card, where the arrow keys scroll it.
    private void OnCardFocusRequested(object? sender, EventArgs e) => _ = NeedsYouCardScroll.Focus(NavigationMethod.Tab);

    // Ctrl+Enter sends the reason and Esc closes the box; any other key is the box's own. The shell's single keys never see a key typed in a field.
    private void OnReasonKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _page?.Bar.Cancel();
            e.Handled = true;
        }
        else if (e is { Key: Key.Enter, KeyModifiers: KeyModifiers.Control or KeyModifiers.Meta })
        {
            _ = _page?.Bar.Send();
            e.Handled = true;
        }
    }

    private void OnApprove(object? sender, RoutedEventArgs e) => _ = _page?.Bar.PressApprove();

    private void OnTerminal(object? sender, RoutedEventArgs e) => _ = _page?.Bar.CopyTerminal();

    private void OnSendBack(object? sender, RoutedEventArgs e) => _ = _page?.Bar.PressSendBack();

    private void OnSend(object? sender, RoutedEventArgs e) => _ = _page?.Bar.Send();

    private void OnCancel(object? sender, RoutedEventArgs e) => _page?.Bar.Cancel();

    private void OnAnswerMoreTries(object? sender, RoutedEventArgs e) => _page?.Answer?.Choose(PausedAnswerOption.MoreTries);

    private void OnAnswerHint(object? sender, RoutedEventArgs e) => _page?.Answer?.Choose(PausedAnswerOption.Hint);

    private void OnAnswerStop(object? sender, RoutedEventArgs e) => _page?.Answer?.Choose(PausedAnswerOption.Stop);

    private void OnAnswerFewer(object? sender, RoutedEventArgs e) => _page?.Answer?.Decrease();

    private void OnAnswerMore(object? sender, RoutedEventArgs e) => _page?.Answer?.Increase();

    private void OnAnswerSend(object? sender, RoutedEventArgs e) => _ = _page?.Answer?.Send();

    private void OnHandBack(object? sender, RoutedEventArgs e) => _ = _page?.Takeover?.HandBack();

    private void OnNotYet(object? sender, RoutedEventArgs e) => _page?.Takeover?.NotYet();

    private void OnCancelRun(object? sender, RoutedEventArgs e) => _page?.Takeover?.AskCancel();

    private void OnCancelConfirm(object? sender, RoutedEventArgs e) => _ = _page?.Takeover?.ConfirmCancel();

    private void OnCancelBackOut(object? sender, RoutedEventArgs e) => _page?.Takeover?.BackOutOfCancel();

    private void OnDropItem(object? sender, RoutedEventArgs e) => _ = _page?.Bar.RequestDrop();

    private void OnDropConfirm(object? sender, RoutedEventArgs e) => _ = _page?.Bar.ConfirmDrop();

    // A row is seen once it has been on screen and focused, in the active window, for a second (the list's rule, as on Home).
    private void Look(RoutedEventArgs e, bool looking)
    {
        if (_page is not null && (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<NeedsYouInboxRow>().FirstOrDefault() is { RowId.Length: > 0 } row)
        {
            _page.Watch(row.RowId, looking && row.IsEffectivelyVisible && TopLevel.GetTopLevel(this) is Window { IsActive: true });
        }
    }

    private void OnRowSelected(object? sender, RoutedEventArgs e)
    {
        if (e.Source is NeedsYouInboxRow { RowId.Length: > 0 } row)
        {
            _page?.Select(row.RowId);
        }
    }

    private void OnCrumbHome(object? sender, RoutedEventArgs e) => _page?.Leave();

    private void OnItem(object? sender, RoutedEventArgs e) => _page?.OpenItem();

    private void OnCopyReference(object? sender, RoutedEventArgs e) => _ = _page?.CopyReference(CancellationToken.None);

    private void OnShowHeld(object? sender, RoutedEventArgs e) => _page?.ShowHeld();

    private void OnListEntered(object? sender, PointerEventArgs e) => SetHold(holding: true);

    private void OnListExited(object? sender, PointerEventArgs e) => SetHold(holding: false);

    private void SetHold(bool holding) => _page?.PointerOver = holding;

    private void OnAction(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: NeedsYouActionControl { Action: var action } })
        {
            _page?.Press(action);
        }
    }
}
