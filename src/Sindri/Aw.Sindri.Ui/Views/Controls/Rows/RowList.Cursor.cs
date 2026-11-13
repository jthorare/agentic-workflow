using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>Keeps the cursor row's focus through edits of <see cref="RowList"/> (#2978): an edit that leaves the row alone changes nothing; one that removes it moves focus to the row at its place.</summary>
internal sealed partial class RowList
{
    private string? _pendingCursor;

    /// <summary>The run index focus falls to when the cursor row is gone; -1 when the row's key is enough.</summary>
    private int PendingFallback { get; set; } = -1;

    // The cursor row's key, when keyboard focus is in the list. The first edit of a burst takes it; EndEdit restores focus once, after the burst, only if it was lost.
    private string? BeginCursor(int fallback, bool lookupIndex)
    {
        if (_pendingCursor is not null || !IsKeyboardFocusWithin || FocusedRow() is not { } item)
        {
            return null;
        }

        PendingFallback = fallback >= 0 ? fallback : (lookupIndex ? _run.Entries.IndexOf(item) : -1);
        return item.Key;
    }

    private void EndEdit(string? cursor)
    {
        UpdateSticky();
        AnnounceRowCount();
        if (cursor is null)
        {
            return;
        }

        _pendingCursor = cursor;
        Dispatcher.UIThread.Post(RestoreCursor, DispatcherPriority.Loaded);
    }

    private IRowItem? FocusedRow() => (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual)?.FindAncestorOfType<Row>(includeSelf: true)?.DataContext as IRowItem;

    // Focus stays on its row; when that row left, it goes to the row now at its place, or the one before it.
    private void RestoreCursor()
    {
        var key = _pendingCursor;
        var fallback = PendingFallback;
        _pendingCursor = null;
        PendingFallback = -1;
        if (key is null || !_attached || FocusIsKept(key))
        {
            return;
        }

        var index = _run.Entries.ToList().FindIndex(e => e is IRowItem row && string.Equals(row.Key, key, StringComparison.Ordinal));
        if (index < 0)
        {
            index = NearestRow(fallback);
        }

        if (index >= 0)
        {
            RowKeys.FocusRow(this, index);
        }
        else
        {
            _ = Focus(NavigationMethod.Directional);   // no row is left: the list keeps the focus, so Esc and Space still reach the page
        }
    }

    // Focus is on a group header (the fold keys put it there), or still on the cursor row.
    private bool FocusIsKept(string key) =>
        (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual)?.FindAncestorOfType<RowGroupHeader>(includeSelf: true) is not null
        || (FocusedRow() is { } still && string.Equals(still.Key, key, StringComparison.Ordinal));

    // The first row at or after index, else the last row before it; -1 when the list has no row.
    private int NearestRow(int index)
    {
        if (index < 0 || _run.Entries.Count == 0)
        {
            return -1;
        }

        var at = Math.Min(index, _run.Entries.Count - 1);
        while (at < _run.Entries.Count && !IsRowIndex(at))
        {
            at++;
        }

        while (at >= _run.Entries.Count || (at >= 0 && !IsRowIndex(at)))
        {
            at--;
        }

        return at;
    }

    // The peek closed: focus was on its close button or its pane, which is gone now, so it goes back to the row that was peeked.
    private void ReturnFocusToSelected()
    {
        if (RowRun.KeyOf(SelectedItem) is { } key && _attached)
        {
            Dispatcher.UIThread.Post(() => FocusPeekedRow(key), DispatcherPriority.Loaded);
        }
    }

    private void FocusPeekedRow(string key)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focused is { IsEffectivelyVisible: true } && focused.FindAncestorOfType<PeekPane>(includeSelf: true) is null)
        {
            return;   // focus is somewhere live that isn't the pane: leave it
        }

        var index = _run.Entries.ToList().FindIndex(e => e is IRowItem row && string.Equals(row.Key, key, StringComparison.Ordinal));
        if (index >= 0)
        {
            RowKeys.FocusRow(this, index);
        }
    }
}
