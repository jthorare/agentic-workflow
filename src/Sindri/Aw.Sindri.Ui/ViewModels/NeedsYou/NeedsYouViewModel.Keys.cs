namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The Needs-you list's keys (U3' T3): J and K move, A runs the primary, X turns a decision down, Y copies the command,
/// Enter opens the sheet and Esc closes it. The keys raise events; the shell runs the action as a button press would.
/// A key never runs a protected or pending action, and nothing is taken for <see cref="KeyQuiet"/> after the selection
/// moved on its own, so a row clearing under the user's hand can't turn the next key into an action on another row.
/// </summary>
internal sealed partial class NeedsYouViewModel
{
    /// <summary>How long keys are ignored after the selection moved on its own (a row cleared under the user's hand).</summary>
    internal static readonly TimeSpan KeyQuiet = TimeSpan.FromMilliseconds(500);

    /// <summary>Raised when a key asks to run an action; the shell runs it as a button press would.</summary>
    internal event EventHandler<NeedsYouActionEventArgs>? ActionRequested;

    /// <summary>Raised when a key asks to copy a row's command.</summary>
    internal event EventHandler<NeedsYouCopyEventArgs>? CopyRequested;

    /// <summary>Raised when Enter asks to open a row's sheet.</summary>
    internal event EventHandler<NeedsYouRowEventArgs>? SheetRequested;

    /// <summary>Raised when Esc closed the sheet: focus goes back to the row.</summary>
    internal event EventHandler<NeedsYouRowEventArgs>? FocusRequested;

    /// <summary>The row whose sheet is open; null for none.</summary>
    internal string? SheetRowId { get; private set; }

    /// <summary>Selects a row and notes its sheet is open (a deep link opened it).</summary>
    /// <param name="id">The row's id.</param>
    internal void OpenSheetOn(string id)
    {
        Select(id);
        SheetRowId = id;
        OnChanged(nameof(SheetRow));
    }

    /// <summary>Another sheet replaced the row's: the row's open marker goes (a key must not act as if its sheet showed).</summary>
    internal void ForgetSheet()
    {
        if (SheetRowId is null)
        {
            return;
        }

        SheetRowId = null;
        OnChanged(nameof(SheetRow));
    }

    /// <summary>A control of a row was pressed: the same request a key makes.</summary>
    /// <param name="rowId">The row's id.</param>
    /// <param name="action">The control's action.</param>
    internal void Press(string rowId, NeedsYouAction action)
    {
        if (Row(rowId) is { } row && action.IsReady)
        {
            _ = Act(row, action);
        }
    }

    /// <summary>Handles a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>True when the list used it.</returns>
    internal bool Press(NeedsYouKey key)
    {
        // Esc closes a sheet; it is no action on a row, so the quiet after an automatic move does not apply (KeyQuiet guards actions only).
        if (key == NeedsYouKey.Escape)
        {
            return CloseSheet();
        }

        if (_clock.GetUtcNow() - _lastAutoMove < KeyQuiet)
        {
            return false;
        }

        var row = SelectedId is { } selected ? Row(selected) : null;
        return key switch
        {
            NeedsYouKey.J => Move(1),
            NeedsYouKey.K => Move(-1),
            NeedsYouKey.Escape => false,
            _ when row is null => false,
            NeedsYouKey.A => RunPrimary(row),
            NeedsYouKey.X => RunReject(row),
            NeedsYouKey.Y => Copy(row),
            NeedsYouKey.Enter => OpenSheet(row),
            _ => false,
        };
    }

    private bool Move(int step)
    {
        var shown = Rows;
        if (shown.Count == 0)
        {
            return false;
        }

        var at = -1;
        for (var i = 0; i < shown.Count; i++)
        {
            if (string.Equals(shown[i].Id, SelectedId, StringComparison.Ordinal))
            {
                at = i;
            }
        }

        SetSelected(shown[at < 0 ? 0 : Math.Clamp(at + step, 0, shown.Count - 1)].Id);
        return true;
    }

    // A runs the row's primary control when the app may: ready, not behind a rebuild, and for a decision only with its sheet
    // open (the sheet shows what is being accepted). A protected or pending primary is never run.
    private bool RunPrimary(NeedsYouRowViewModel row)
    {
        var decision = row.Entry.DecisionId is not null;
        var primary = row.Controls.FirstOrDefault(c => c.IsPrimary && c.IsEnabled && !c.Action.IsProtected);   // a protected action is a click, never a key
        return primary is not null && (!decision || (primary.Action.OptionId.Length > 0 && string.Equals(SheetRowId, row.Id, StringComparison.Ordinal))) && Act(row, primary.Action);
    }

    // X picks the option the API marks rejectLike; a row without one has no X.
    private bool RunReject(NeedsYouRowViewModel row)
    {
        var reject = row.Controls.FirstOrDefault(c => c.Action.RejectLike && c.IsEnabled);
        return reject is not null && Act(row, reject.Action);
    }

    private bool Act(NeedsYouRowViewModel row, NeedsYouAction action)
    {
        Seen.Acted(row.Id);
        ActionRequested?.Invoke(this, new NeedsYouActionEventArgs(row.Id, action));
        return true;
    }

    /// <summary>The listed row with an id.</summary>
    /// <param name="id">The row's id.</param>
    /// <returns>The row, or null when it is not listed.</returns>
    internal NeedsYouRowViewModel? Row(string id) => Seen.Rows.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));

    /// <summary>The listed row that stands for a decision.</summary>
    /// <param name="decisionId">The decision's id.</param>
    /// <returns>The row, or null when no listed row stands for it.</returns>
    internal NeedsYouRowViewModel? RowOfDecision(string decisionId) => Seen.Rows.FirstOrDefault(r => string.Equals(r.Entry.DecisionId, decisionId, StringComparison.Ordinal));

    private bool Copy(NeedsYouRowViewModel row)
    {
        if (row.Cli.Length == 0)
        {
            return false;
        }

        CopyRequested?.Invoke(this, new NeedsYouCopyEventArgs(row.Id, row.Cli));
        return true;
    }

    private bool OpenSheet(NeedsYouRowViewModel row)
    {
        SheetRowId = row.Id;
        SheetRequested?.Invoke(this, new NeedsYouRowEventArgs(row.Id));
        return true;
    }

    /// <summary>The sheet closed (Esc, its close button): its row is no longer open, and focus goes back to the row.</summary>
    /// <returns>True when a row's sheet was open.</returns>
    internal bool CloseSheet()
    {
        if (SheetRowId is not { } open)
        {
            return false;
        }

        SheetRowId = null;
        OnChanged(nameof(SheetRow));
        var gone = !Seen.Rows.Any(r => string.Equals(r.Id, open, StringComparison.Ordinal));
        FocusRequested?.Invoke(this, new NeedsYouRowEventArgs(gone && SelectedId is { } next ? next : open));   // a row decided from its sheet has cleared: focus goes to the row now selected
        return true;
    }
}
