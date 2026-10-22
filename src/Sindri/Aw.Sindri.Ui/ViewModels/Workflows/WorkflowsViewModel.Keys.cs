using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The Workflows page's keys (<see cref="WorkflowsViewModel"/>, continued; U8 T7, UI spec §8): J / K walk the drawn rows across the groups, and
/// Enter, A and X act on the selected row. The selection is an address: a re-read keeps it on the same instance wherever the row moved,
/// and clears it when the row is gone, so no key ever acts on a row the human is not on.
/// </summary>
internal sealed partial class WorkflowsViewModel
{
    /// <summary>Raised when the page should put focus on a row's frame: a key moved the selection, or a re-read changed the rows under it.</summary>
    internal event EventHandler<WorkflowFocusEventArgs>? FocusRequested;

    /// <summary>The address of the row the keys act on; null for none.</summary>
    public string? SelectedAddress { get; private set => Set(ref field, value); }

    /// <summary>The row the keys act on; null for none, and for one in a group the human collapsed.</summary>
    public InstanceRowViewModel? SelectedRow => Walk.FirstOrDefault(r => string.Equals(r.Address, SelectedAddress, StringComparison.Ordinal));

    /// <summary>The words a key said when it only names a later phase; empty otherwise.</summary>
    public string KeyNote { get; private set { Set(ref field, value); OnChanged(nameof(HasKeyNote)); } } = string.Empty;

    /// <summary>The live region that says the note: flag first, text second, never empty.</summary>
    public LiveAnnouncer KeyNoteVoice { get; } = new(Labels.KeyNotesResting);

    /// <summary>True when a key left a note.</summary>
    public bool HasKeyNote => KeyNote.Length > 0;

    // Every row the page holds, and the rows J / K walk: those drawn (a collapsed group's are not).
    private IEnumerable<InstanceRowViewModel> Shown => Groups.SelectMany(g => g.Rows).Concat(DoneRows);

    private List<InstanceRowViewModel> Walk => ViewMode == WorkflowViewMode.Board
        ? [.. Lanes.SelectMany(l => l.Cards)]
        : [.. Groups.Where(g => g.IsExpanded).SelectMany(g => g.Rows), .. IsDoneExpanded ? DoneRows : []];

    /// <summary>Selects a row by its address.</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>True when a row of that address is shown.</returns>
    internal bool Select(string address)
    {
        if (!Shown.Any(r => string.Equals(r.Address, address, StringComparison.Ordinal)))
        {
            return false;
        }

        if (!string.Equals(SelectedAddress, address, StringComparison.Ordinal))
        {
            ClearNote();   // a note belongs to the row it was said on; focus arriving on the selected row leaves it
        }

        SelectedAddress = address;
        Mark();
        return true;
    }

    /// <summary>Clears the selection.</summary>
    internal void Deselect()
    {
        ClearNote();
        SelectedAddress = null;
        Mark();
    }

    /// <summary>The Start button's words (the catalog's label for StartInstance).</summary>
    public string StartLabel { get; } = Labels.StartWorkflow;

    /// <summary>Raised when the human asks to start a workflow (N, the head's Start button): the shell opens the Start form.</summary>
    internal event EventHandler? StartRequested;

    /// <summary>Asks for the Start form.</summary>
    internal void RequestStart() => StartRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>One single key on the page.</summary>
    /// <param name="key">J, K, Enter, A, X, B or N.</param>
    /// <returns>True when the key was used.</returns>
    internal bool Press(string key)
    {
        if (key is not "B")
        {
            ClearNote();
        }

        switch (key)
        {
            case "J" or "K":
                return Move(key == "J" ? 1 : -1);
            case "N":
                RequestStart();
                return true;
            case "B":
                Say(Labels.HandBackArrives);   // never a dead key: the note says where hand-back is
                return true;
            default:
                return PressOnPage(key) || (SelectedRow is { } row && PressOn(row, key));
        }
    }

    // V switches the view and / goes to the search box: keys of the page, whatever is selected.
    private bool PressOnPage(string key)
    {
        switch (key)
        {
            case "V":
                ToggleView();
                return true;
            case "/":
                SearchFocusRequested?.Invoke(this, EventArgs.Empty);
                return true;
            default:
                return false;
        }
    }

    // Enter opens the row; A answers it (a protected answer is the lock's popover, a desk task its pending output); X asks to cancel it.
    private bool PressOn(InstanceRowViewModel row, string key)
    {
        switch (key)
        {
            case "Enter":
                OpenRequested?.Invoke(this, new OpenRequestedEventArgs(row.Link));
                return true;
            case "A" when row.Protected is { } locked:
                locked.SetOpen(open: true);
                return true;
            case "A" when row.CanAnswerAtDesk:
                OpenRequested?.Invoke(this, new OpenRequestedEventArgs(row.Link, FrameIntent.PendingOutput));
                return true;
            case "X" when row.CanCancel:
                OpenRequested?.Invoke(this, new OpenRequestedEventArgs(row.Link, FrameIntent.AskCancel));
                return true;
            default:
                return false;
        }
    }

    // A note is seen at once and said once: the live region goes live before its words change, and goes dark by its flag, never by an empty text.
    private void Say(string words)
    {
        KeyNote = words;
        KeyNoteVoice.Say(words);
    }

    /// <summary>Takes the note away (the selection moved, the page changed, a sheet opened).</summary>
    internal void ClearNote()
    {
        KeyNoteVoice.Quiet();
        KeyNote = string.Empty;
    }

    // From no row J goes to the first and K to the last; at either end the selection stays.
    private bool Move(int step)
    {
        var rows = Walk;
        if (rows.Count == 0)
        {
            return false;
        }

        var at = rows.FindIndex(r => string.Equals(r.Address, SelectedAddress, StringComparison.Ordinal));
        var to = at >= 0 ? Math.Clamp(at + step, 0, rows.Count - 1) : NearestDrawn(rows, step);
        SelectedAddress = rows[to].Address;
        Mark();
        FocusRequested?.Invoke(this, new WorkflowFocusEventArgs(rows[to].Address, onlyIfLost: false));
        return true;
    }

    // From no row J goes to the first and K to the last; from a row in a group the human collapsed, to the nearest drawn row in the key's direction.
    private int NearestDrawn(List<InstanceRowViewModel> drawn, int step)
    {
        var all = Shown.Select(r => r.Address).ToList();
        var from = all.FindIndex(a => string.Equals(a, SelectedAddress, StringComparison.Ordinal));
        if (from < 0)
        {
            return step > 0 ? 0 : drawn.Count - 1;
        }

        var order = drawn.Select(r => all.IndexOf(r.Address)).ToList();
        var next = step > 0 ? order.FindIndex(i => i > from) : order.FindLastIndex(i => i < from);
        return next >= 0 ? next : (step > 0 ? drawn.Count - 1 : 0);
    }

    // After a read: the selection stays on the same address (and asks for focus back if the re-read took it away with the old rows), or, when
    // its row is gone, moves to the row that followed it in the walk (the one before it at the end) so focus has somewhere to go.
    private void Reselect(IReadOnlyList<string> before)
    {
        if (SelectedAddress is { } at)
        {
            if (!Shown.Any(r => string.Equals(r.Address, at, StringComparison.Ordinal)))
            {
                SelectedAddress = Successor(before, at);
            }

            if (SelectedAddress is { } now)
            {
                FocusRequested?.Invoke(this, new WorkflowFocusEventArgs(now, onlyIfLost: true));
            }
        }

        Mark();
    }

    private List<string> WalkAddresses() => [.. Walk.Select(r => r.Address)];

    private string? Successor(IReadOnlyList<string> before, string gone)
    {
        var now = new HashSet<string>(Walk.Select(r => r.Address), StringComparer.Ordinal);
        var at = before.ToList().FindIndex(a => string.Equals(a, gone, StringComparison.Ordinal));
        return at < 0 ? null : before.Skip(at + 1).FirstOrDefault(now.Contains) ?? before.Take(at).Reverse().FirstOrDefault(now.Contains);
    }

    private void Mark()
    {
        foreach (var row in Shown)
        {
            row.IsSelected = string.Equals(row.Address, SelectedAddress, StringComparison.Ordinal);
        }
    }
}
