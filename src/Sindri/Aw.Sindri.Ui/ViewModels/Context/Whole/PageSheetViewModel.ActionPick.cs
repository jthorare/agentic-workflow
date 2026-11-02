namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// An action row's "Pick another" (<see cref="PageSheetViewModel"/>, continued, pages spec §3.6): a search over the map actions of the page's map surface that repoints one row.
/// It reads once when it opens and filters what it read by name as the typing rests; the text under the row lives on <see cref="PageSheetActionViewModel"/>.
/// </summary>
internal sealed partial class PageSheetViewModel
{
    private int _actionGeneration;
    private Task<IReadOnlyList<PageMatch>?> _actionSource = Task.FromResult<IReadOnlyList<PageMatch>?>(null);

    /// <summary>Gets the latest action search: it ends once its answer is drawn.</summary>
    internal Task ActionPicking { get; private set; } = Task.CompletedTask;

    /// <summary>Gets the row whose search is open, or null.</summary>
    internal PageSheetActionViewModel? PickingRow { get; private set; }

    /// <summary>Raised when focus goes into a row's search box.</summary>
    internal event EventHandler<ActionPickEventArgs>? ActionPickBoxFocusRequested;

    /// <summary>Raised when focus goes back to a row's Pick another button.</summary>
    internal event EventHandler<ActionPickEventArgs>? ActionPickButtonFocusRequested;

    /// <summary>Opens a row's search and reads the page's actions; any other search closes.</summary>
    /// <param name="row">The row.</param>
    internal void OpenActionPick(PageSheetActionViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (!IsReady || _draft?.Surface is not { } surface || ReferenceEquals(PickingRow, row))
        {
            return;
        }

        EndPick();
        EndActionPick();
        PickingRow = row;
        row.IsPicking = true;
        _actionSource = _writes.Actions(surface, CancellationToken.None);
        ActionPicking = ReadActions(row);
        ActionPickBoxFocusRequested?.Invoke(this, new ActionPickEventArgs(row));
    }

    /// <summary>Closes the open row search without a pick.</summary>
    /// <returns>True when a search was open.</returns>
    internal bool CloseActionPick()
    {
        if (PickingRow is not { } row)
        {
            return false;
        }

        EndActionPick();
        ActionPickButtonFocusRequested?.Invoke(this, new ActionPickEventArgs(row));
        return true;
    }

    /// <summary>Points the open row at a found map action and closes its search.</summary>
    /// <param name="match">The row of <see cref="PageSheetActionViewModel.PickRows"/>.</param>
    internal void ChooseAction(PageMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (PickingRow is not { } row)
        {
            return;
        }

        row.Action = match.Key;
        EndActionPick();
        OnChanged(nameof(HasChanges));
        ActionPickButtonFocusRequested?.Invoke(this, new ActionPickEventArgs(row));
    }

    // A row that goes away takes its search with it.
    private void ReleaseActionPick(PageSheetActionViewModel row)
    {
        if (ReferenceEquals(PickingRow, row))
        {
            EndActionPick();
        }
    }

    private void EndActionPick()
    {
        _actionGeneration++;   // a read or a rest still running is dropped
        PickingRow?.ClearPick();
        PickingRow = null;
    }

    private void OnActionQuery(PageSheetActionViewModel row)
    {
        if (ReferenceEquals(PickingRow, row))
        {
            ActionPicking = FilterAfterRest(row);
        }
    }

    // Typing re-filters what the search read; it doesn't read again.
    private async Task FilterAfterRest(PageSheetActionViewModel row)
    {
        var generation = ++_actionGeneration;
        await Task.Delay(PickDebounce, _clock, CancellationToken.None).ConfigureAwait(true);
        if (generation == _actionGeneration)
        {
            Show(row, await _actionSource.ConfigureAwait(true));
        }
    }

    private async Task ReadActions(PageSheetActionViewModel row)
    {
        var generation = ++_actionGeneration;
        row.PickStatus = Labels.PageSheetPickSearching;
        var found = await _actionSource.ConfigureAwait(true);
        if (generation == _actionGeneration)
        {
            Show(row, found);
        }
    }

    // Left out: the row's own action and every action another row of this sheet holds (saved or just picked), so two rows never point at one map action.
    private void Show(PageSheetActionViewModel row, IReadOnlyList<PageMatch>? found)
    {
        row.PickRows.Clear();
        if (found is null)
        {
            row.PickStatus = Labels.PageSheetActionPickFailed;
            return;
        }

        var text = row.PickQuery.Trim();
        var held = Actions.Select(static a => a.Action).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var match in found.Where(m => !held.Contains(m.Key) && m.Name.Contains(text, StringComparison.OrdinalIgnoreCase)).Take(PickLimit))
        {
            row.PickRows.Add(match);
        }

        row.PickStatus = row.PickRows.Count > 0 ? string.Empty : text.Length == 0 ? Labels.PageSheetActionPickNoActions : Labels.PageSheetActionPickNone(text);
    }
}
