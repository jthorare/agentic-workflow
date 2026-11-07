using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>The Linear list's keys (<see cref="LinearWorkViewModel"/>, continued; Linear hub spec §3 "Accessibility"): J / K select, Enter opens, / focuses the filter bar.</summary>
internal sealed partial class LinearWorkViewModel
{
    /// <summary>The key of the selected row (J / K, or focus arriving on a row); null with none.</summary>
    internal string? SelectedKey { get; private set; }

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
