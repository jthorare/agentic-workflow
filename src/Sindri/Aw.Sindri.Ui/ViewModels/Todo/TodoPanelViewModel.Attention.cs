using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The panel's attention rows (<see cref="TodoPanelViewModel"/>, continued; todo spec §3.1, §3.3, §3.4, D11, D13): the open conflicts, oldest first, then the hold. One is shown, folded to a line
/// or open to its full form; the rest fold into "Sort out N more". They count inside the five rows, at most two slots. 1 keeps Sindri's side (a hold: puts the lines back in the note), 2 keeps
/// Obsidian's (a hold: applies in Sindri); a pick shows a toast with a 10 s countdown whose Undo reopens the row.
/// </summary>
internal sealed partial class TodoPanelViewModel
{
    private static readonly TimeSpan _keyQuiet = TimeSpan.FromMilliseconds(500);

    private TodoNoteView? _note;
    private List<AttentionRowViewModel> _queue = [];
    private HashSet<string>? _queueIds;
    private string? _shownId;
    private string? _openId;
    private DateTimeOffset _queueChangedAt = DateTimeOffset.MinValue;

    /// <summary>The attention row that is shown, or null when nothing needs sorting out.</summary>
    public AttentionRowViewModel? Attention { get; private set => Set(ref field, value); }

    /// <summary>True while an attention row is shown.</summary>
    public bool HasAttention => Attention is not null;

    /// <summary>True while the shown row is open to its full form.</summary>
    public bool IsAttentionOpen => Attention is { IsOpen: true };

    /// <summary>"Sort out 2 more" when more attention rows wait behind the shown one; empty otherwise.</summary>
    public string MoreText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"2 more to sort out" for a screen reader; empty when none wait.</summary>
    public string MoreName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The key beside "Sort out N more" ("Enter").</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string MoreKey => Labels.TodoAttnMoreKey;
#pragma warning restore CA1822, MA0041

    /// <summary>True while attention rows wait behind the shown one.</summary>
    public bool HasMore => MoreText.Length > 0;

    /// <summary>Raised after a pick Sindri accepted: the toast with its countdown and its Undo.</summary>
    internal event EventHandler<TodoPickedEventArgs>? Picked;

    /// <summary>Raised when focus should move onto the open attention row's first button.</summary>
    internal event EventHandler? AttentionButtonsRequested;

    /// <summary>Enter on the attention row: opens it to its full form and moves focus onto its first button.</summary>
    /// <returns>True when a row was shown to open.</returns>
    internal bool OpenAttention()
    {
        if (Attention is not { } row)
        {
            return false;
        }

        _openId = row.Id;
        row.IsOpen = true;
        OnChanged(nameof(IsAttentionOpen));
        AttentionButtonsRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Esc on the open attention row: folds it back to its line.</summary>
    /// <returns>True when a row was open.</returns>
    internal bool CloseAttention()
    {
        if (Attention is not { IsOpen: true } row)
        {
            return false;
        }

        (_openId, row.IsOpen) = (null, false);
        OnChanged(nameof(IsAttentionOpen));
        return true;
    }

    /// <summary>Enter on the "Sort out N more" line: shows the next attention row; the one shown folds.</summary>
    /// <returns>True when another row waited.</returns>
    internal bool OpenNextAttention()
    {
        if (Attention is not { } shown || _queue.Count < 2)
        {
            return false;
        }

        var at = _queue.FindIndex(r => string.Equals(r.Id, shown.Id, StringComparison.Ordinal));
        (_shownId, _openId) = (_queue[(at + 1) % _queue.Count].Id, null);
        Show();
        return true;
    }

    /// <summary>X, Space or Delete on an attention row: nothing is sent; a conflict row says "Pick a version first".</summary>
    /// <returns>True when an attention row was shown (the key is spent).</returns>
    internal bool NoPick()
    {
        if (Attention is not { } row)
        {
            return false;
        }

        row.Notice = row.IsConflict ? Labels.TodoAttnPickFirst : string.Empty;
        return true;
    }

    // How many of the five slots the attention area takes: its line, and the "Sort out N more" line when more than one waits.
    private static int SlotsFor(int queued) => queued == 0 ? 0 : queued == 1 ? 1 : 2;

    // The open conflicts, oldest first as the note lists them (a todo the note doesn't name follows), then the hold. Without a note read there is nothing to draw.
    private List<AttentionRowViewModel> AttentionQueue(IReadOnlyList<TodoView> todos)
    {
        if (_note is not { } note)
        {
            return [];
        }

        var conflicted = todos.Where(t => t.Conflict is not null).ToList();
        List<TodoView> ordered =
        [
            .. note.Conflicts.Select(id => conflicted.Find(t => string.Equals(t.Id, id, StringComparison.Ordinal))).OfType<TodoView>(),
            .. conflicted.Where(t => !note.Conflicts.Contains(t.Id, StringComparer.Ordinal)),
        ];
        List<AttentionRowViewModel> queue = [.. ordered.Select(AttentionRowViewModel.ForConflict)];
        if (note.Hold is { } hold)
        {
            queue.Add(AttentionRowViewModel.ForHold(hold));
        }

        return queue;
    }

    // Shows one row of the queue: the one the owner opened or moved to, else the first. A row whose words did not change stays as it is, with its open form and its notice.
    private void ShowAttention(List<AttentionRowViewModel> queue)
    {
        NoteQueueChange(queue);
        for (var i = 0; i < queue.Count; i++)
        {
            queue[i] = KeptRow(queue[i]);
        }

        _queue = queue;
        var row = queue.Find(r => string.Equals(r.Id, _shownId, StringComparison.Ordinal)) ?? queue.FirstOrDefault();
        _shownId = row?.Id;
        _openId = row is not null && string.Equals(_openId, row.Id, StringComparison.Ordinal) ? _openId : null;
        row?.IsOpen = _openId is not null;

        var more = Math.Max(queue.Count - 1, 0);
        (MoreText, MoreName) = more == 0 ? (string.Empty, string.Empty) : (Labels.TodoAttnMore(more), Labels.TodoAttnMoreName(more));
        Attention = row;
        OnChanged(nameof(HasAttention));
        OnChanged(nameof(IsAttentionOpen));
        OnChanged(nameof(HasMore));
    }

    // A sync or an agent changed what needs sorting out: the digits are quiet for a moment (D13). The first read of the note is not a change.
    private void NoteQueueChange(List<AttentionRowViewModel> queue)
    {
        var ids = new HashSet<string>(queue.Select(r => r.Id), StringComparer.Ordinal);
        if (_queueIds is not null && !ids.SetEquals(_queueIds))
        {
            _queueChangedAt = _clock.GetUtcNow();
        }

        _queueIds = ids;
    }

    // The row already shown for that id when its words did not change (it keeps its open form and its notice), else the new one.
    private AttentionRowViewModel KeptRow(AttentionRowViewModel fresh) =>
        _queue.Find(r => string.Equals(r.Id, fresh.Id, StringComparison.Ordinal)) is { } kept && string.Equals(kept.Signature, fresh.Signature, StringComparison.Ordinal) ? kept : fresh;
}
