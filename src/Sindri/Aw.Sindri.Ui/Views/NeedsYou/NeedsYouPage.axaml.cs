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

    public NeedsYouPage()
    {
        InitializeComponent();
        AddHandler(NeedsYouInboxRow.SelectEvent, OnRowSelected);
        AddHandler(GotFocusEvent, (_, e) => Look(e, looking: true));
        AddHandler(LostFocusEvent, (_, e) => Look(e, looking: false));
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
        _page = DataContext as NeedsYouPageViewModel;
    }

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
