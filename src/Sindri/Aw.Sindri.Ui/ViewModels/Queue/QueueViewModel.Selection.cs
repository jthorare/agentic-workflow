namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>
/// Selection, focus and the polite region (<see cref="QueueViewModel"/>, continued; UI spec §3.3): one selection is the
/// focus, J / K walk the page's own rows, a refresh keeps the selection by id and, when its row cleared, moves it to the
/// next row, says so once and asks for focus there; a filter change that hides the selection clears it silently.
/// </summary>
internal sealed partial class QueueViewModel
{
    /// <summary>Selects a row by id (a click, focus arriving on it); focus is already there.</summary>
    /// <param name="itemId">The row's id.</param>
    /// <param name="scope">The page the row is on: a refresh that clears it looks for its successor there.</param>
    internal void Select(string itemId, QueueScope scope)
    {
        (_scope, _onPage, _reveal) = (scope, true, null);
        SelectedId = itemId;
        Mark();
    }

    /// <summary>
    /// Selects a row that was just created and asks the view to focus (and so scroll to) it. The row arrives through the event
    /// stream, so when it isn't drawn yet it is held and taken by the refresh that brings it; any selection made meanwhile drops it.
    /// </summary>
    /// <param name="itemId">The new item's id.</param>
    internal void Reveal(string itemId)
    {
        _reveal = itemId;
        Revealed();
    }

    // The held row is drawn on the page shown: select it and focus it, once.
    private void Revealed()
    {
        if (_reveal is { } id && Drawn(id))
        {
            Select(id, _scope);
            FocusRequested?.Invoke(this, new FocusRequestedEventArgs(id, onlyIfLost: false));
        }
    }

    /// <summary>True when the selected row is the first one Home draws (K goes up to Needs you from it).</summary>
    internal bool IsFirstOnHome => Navigable(QueueScope.Home) is [{ } first, ..] && string.Equals(first.Id, SelectedId, StringComparison.Ordinal);

    /// <summary>Selects no row (the selection moved to another list on Home).</summary>
    internal void ClearSelection()
    {
        SelectedId = null;
        Mark();
    }

    /// <summary>The shell shows a page: the selection's scope follows it (silently); a page without rows (null) is no scope.</summary>
    /// <param name="scope">The page shown, or null for a page that draws no rows.</param>
    internal void PageShown(QueueScope? scope)
    {
        _onPage = scope is not null;
        _scope = scope ?? _scope;
    }

    /// <summary>
    /// J / K (§8): the next or previous row of the page the key was pressed on, in the order it draws them; stays at the
    /// ends. Selecting is focusing (§3.3): the view is asked to move focus onto the row.
    /// </summary>
    /// <param name="by">+1 for next, -1 for previous.</param>
    /// <param name="scope">The page the key was pressed on.</param>
    internal void Move(int by, QueueScope scope)
    {
        var rows = Navigable(scope);
        if (rows.Count == 0)
        {
            return;
        }

        var at = rows.FindIndex(r => string.Equals(r.Id, SelectedId, StringComparison.Ordinal));
        var next = at < 0 ? 0 : Math.Clamp(at + by, 0, rows.Count - 1);
        Select(rows[next].Id, scope);
        FocusRequested?.Invoke(this, new FocusRequestedEventArgs(rows[next].Id, onlyIfLost: false));
    }

    /// <summary>True when the selected row is one the page draws, so Enter and Y act on a row the user can see.</summary>
    /// <param name="scope">The page the key was pressed on.</param>
    /// <returns>True when there is a selected row on the page.</returns>
    internal bool HasSelectionOn(QueueScope scope) => Navigable(scope).Exists(r => string.Equals(r.Id, SelectedId, StringComparison.Ordinal));

    // The rows a page draws, in order: Home's two panels, or the Queue page's visible groups that are open. A refresh measures
    // the selection's place by them too: the Queue page's filter doesn't apply to Home's panels (and the other way round).
    private List<ItemRowViewModel> Navigable(QueueScope scope) => scope == QueueScope.Home
        ? [.. RunningRows, .. QueuedRows]
        : [.. Groups.Where(g => g.IsVisible && g.IsExpanded).SelectMany(g => g.Rows)];

    // The list a row sits in on that page: its group on the Queue page, one of Home's two panels on Home.
    private static string Panel(QueueScope scope, string group) => scope == QueueScope.Home && group != "queued" ? "running" : group;

    // A filter change that hides the selected row clears it: nothing hidden stays selected for Enter or Y (no announcement:
    // the human did it).
    private void ClearHidden()
    {
        if (SelectedId is { } id && !Visible().Any(r => string.Equals(r.Id, id, StringComparison.Ordinal)))
        {
            SelectedId = null;
            Mark();
        }
    }

    // A row hidden by the filter is not gone: it is checked against the unfiltered view.
    private void Keep(List<(string Id, string Group)> before, bool byPill)
    {
        if (SelectedId is not { } id)
        {
            return;
        }

        // Served but not drawn on the page shown any more (past Home's cap, in a group Home or the filter doesn't draw): for
        // this page it is cleared, so it passes on like a cleared row. A row that was never drawn here is left alone, and on
        // a page without rows (Decisions, Services...) a served row keeps its selection silently.
        var wasDrawn = before.Exists(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        if (_rows.TryGetValue(id, out var kept) && (!_onPage || !wasDrawn || Drawn(id)))
        {
            Regrouped(id, kept, before);
            return;
        }

        // On a page without rows the departure is no one's to hear or follow: it settles like the pill's press.
        Clear(id, before, byPill || !_onPage);
    }

    // The selected row left the page: the selection passes to the next row (see Keep).
    private void Clear(string id, List<(string Id, string Group)> before, bool byPill)
    {
        var next = Successor(id, before);
        if (byPill)
        {
            SelectedId = next?.Id;
            Mark();
            return;
        }

        _spoken = _spoken is { } held ? Labels.NewAndCleared(held, id) : Labels.Cleared(id);
        SelectedId = next?.Id;
        _movedAt = clock.GetUtcNow();
        Mark();
        if (next is not null)
        {
            FocusRequested?.Invoke(this, new FocusRequestedEventArgs(next.Id, onlyIfLost: true));
        }
    }

    private bool Drawn(string id) => Navigable(_scope).Exists(r => string.Equals(r.Id, id, StringComparison.Ordinal));

    // Still there, but in another group or panel: its element was built anew where it now sits, and focus on the old one is lost.
    private void Regrouped(string id, ItemRowViewModel kept, List<(string Id, string Group)> before)
    {
        var was = before.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));
        if (was.Id is not null && !string.Equals(Panel(_scope, was.Group), Panel(_scope, kept.Group), StringComparison.Ordinal))
        {
            FocusRequested?.Invoke(this, new FocusRequestedEventArgs(id, onlyIfLost: true));
        }
    }

    // The row now at the cleared row's place in the same list, else the list's last row, else the page's first row.
    private ItemRowViewModel? Successor(string id, List<(string Id, string Group)> before)
    {
        var old = before.FirstOrDefault(r => r.Id == id);
        if (old.Id is null)
        {
            return Navigable(_scope).FirstOrDefault();
        }

        var panel = Panel(_scope, old.Group);
        var peers = Navigable(_scope).Where(r => Panel(_scope, r.Group) == panel).ToList();
        var index = before.Where(r => Panel(_scope, r.Group) == panel).ToList().FindIndex(r => r.Id == id);
        return peers.Count == 0 ? Navigable(_scope).FirstOrDefault() : peers[Math.Min(index, peers.Count - 1)];
    }

    // Every row there is, whatever the Queue page shows: Home's panels draw rows the page's filter hides.
    private void Mark()
    {
        foreach (var row in _rows.Values)
        {
            row.IsSelected = string.Equals(row.Id, SelectedId, StringComparison.Ordinal);
        }
    }

    // Says a text through the polite region: dark first, the resting text set, then live, then the text (the flag changes
    // before the text, as the shell's regions do), so the resting text is never spoken and a text is spoken once.
    private void Say(string text)
    {
        if (IsAnnouncing && string.Equals(Announcement, text, StringComparison.Ordinal))
        {
            IsAnnouncing = false;   // the same text again is still a change: dark first, then the text
        }

        if (!IsAnnouncing)
        {
            Announcement = Labels.Sindri;
            IsAnnouncing = true;
        }

        Announcement = text;
    }

    // The region goes dark again at the next refresh (flag first, then the resting text).
    private void Rest()
    {
        if (IsAnnouncing)
        {
            IsAnnouncing = false;
            Announcement = Labels.Sindri;
        }
    }
}
