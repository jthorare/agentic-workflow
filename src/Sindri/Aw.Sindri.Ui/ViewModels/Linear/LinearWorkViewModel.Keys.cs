using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>The Linear list's keys (<see cref="LinearWorkViewModel"/>, continued; Linear hub spec §3 "Accessibility"): J / K select, Enter opens, / focuses the filter bar.</summary>
internal sealed partial class LinearWorkViewModel
{
    /// <summary>The key of the selected row (J / K, or focus arriving on a row); null with none.</summary>
    internal string? SelectedKey { get; private set; }

    private string? _selectWhenShown;
    private bool _selectTakesFocus;

    /// <summary>The issue <see cref="SelectWhenShown"/> waited for did not come (the filters hide it, or the read failed): the pending pick is dropped and the view puts focus on the page's Create button.</summary>
    internal event EventHandler? SelectMissed;

    /// <summary>The `/` key asked for focus in the filter bar.</summary>
    internal event EventHandler? FilterFocusRequested;

    /// <summary>J / K: the next or previous row in display order (Assigned to you, then Your teams), stopping at the ends; with none selected the first row. Focus follows.</summary>
    /// <param name="by">+1 for next, -1 for previous.</param>
    internal void Move(int by)
    {
        var rows = Ordered();
        if (rows.Count == 0)
        {
            return;
        }

        var at = rows.FindIndex(r => string.Equals(r.Key, SelectedKey, StringComparison.Ordinal));
        var next = rows[at < 0 ? 0 : Math.Clamp(at + by, 0, rows.Count - 1)];
        Select(next.Key);
        FocusRequested?.Invoke(this, new FocusRequestedEventArgs(next.Key, onlyIfLost: false));
    }

    /// <summary>Makes a row the selected one (focus is already there).</summary>
    /// <param name="key">The issue key.</param>
    internal void Select(string key)
    {
        if (string.Equals(SelectedKey, key, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var row in Ordered())
        {
            row.IsSelected = string.Equals(row.Key, key, StringComparison.Ordinal);
        }

        SelectedKey = key;
    }

    /// <summary>Enter: asks for the selected row's issue; nothing with no selection.</summary>
    /// <returns>True when a row was selected.</returns>
    internal bool OpenSelected()
    {
        if (SelectedKey is not { } key)
        {
            return false;
        }

        RequestIssue(key);
        return true;
    }

    /// <summary>`/`: asks the view to focus the first filter.</summary>
    internal void FocusFilters() => FilterFocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Selects and focuses a row once the list shows it (a just-created issue): now when it is listed, else after the next read of the list. When that read does not bring it, the wait ends.</summary>
    /// <param name="key">The issue key.</param>
    /// <param name="takeFocus">False when the dialog was closed before the answer came: the row is selected, but focus moves only if it is lost.</param>
    internal void SelectWhenShown(string key, bool takeFocus = true)
    {
        (_selectWhenShown, _selectTakesFocus) = (key, takeFocus);
        if (SelectPending())
        {
            return;
        }

        if (IsLinear && IsActive && _url is { } url)
        {
            Reading = Observed(Settle(key, ReadList(url, _shown?.Token ?? CancellationToken.None), _reads + 1));
        }
        else
        {
            Missed(key);
        }
    }

    // The read SelectWhenShown started is over: a row not shown by now will not be by this wait. A read that replaced it settles the wait itself.
    private async Task Settle(string key, Task read, int number)
    {
        await read.ConfigureAwait(true);
        if (_reads == number)
        {
            Missed(key);
        }
    }

    private void Missed(string key)
    {
        if (!string.Equals(_selectWhenShown, key, StringComparison.Ordinal))
        {
            return;   // shown meanwhile, or a filter change ended the wait
        }

        _selectWhenShown = null;
        if (_selectTakesFocus)
        {
            SelectMissed?.Invoke(this, EventArgs.Empty);
        }
    }

    // The issue named by SelectWhenShown, once a read has brought its row: selected, with focus on it. One shot.
    private bool SelectPending()
    {
        if (_selectWhenShown is not { } key || !Ordered().Exists(r => string.Equals(r.Key, key, StringComparison.Ordinal)))
        {
            return false;
        }

        _selectWhenShown = null;
        Select(key);
        FocusRequested?.Invoke(this, new FocusRequestedEventArgs(key, onlyIfLost: !_selectTakesFocus));
        return true;
    }

    private List<LinearIssueRowViewModel> Ordered() => [.. Mine.Rows, .. Teams.Rows];

    // A read that dropped the selected issue (a filter, a state change) leaves nothing selected.
    private void KeepSelection()
    {
        if (SelectedKey is { } key && !Ordered().Exists(r => string.Equals(r.Key, key, StringComparison.Ordinal)))
        {
            SelectedKey = null;
        }
    }
}
