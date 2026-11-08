using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>
/// The attention rows' picks and their Undo (<see cref="TodoPanelViewModel"/>, continued; todo spec §3.3, §3.4, D11): 1 and 2 call keep or the hold's restore and apply; Sindri's refusals redraw the row
/// with the reason; the toast's Undo reopens the conflict or the hold.
/// </summary>
internal sealed partial class TodoPanelViewModel
{
    private bool _picking;

    /// <summary>1 or 2 on the attention row, or its button: 1 is Sindri's side (a hold: Put them back), 2 is Obsidian's (a hold: Apply).</summary>
    /// <param name="digit">1 or 2.</param>
    /// <returns>A task that completes when Sindri has answered.</returns>
    internal async Task Pick(int digit)
    {
        // A row that arrived under the owner's hands takes no digit for the quiet window (D13); one pick at a time.
        if (Attention is not { } row || _url is not { } url || digit is not (1 or 2) || _picking || _clock.GetUtcNow() - _queueChangedAt < _keyQuiet)
        {
            return;
        }

        (_picking, row.Notice) = (true, string.Empty);
        try
        {
            await (row.IsConflict ? PickConflict(url, row, digit) : PickHold(url, row, digit)).ConfigureAwait(true);
        }
        finally
        {
            _picking = false;
        }
    }

    private async Task PickConflict(Uri url, AttentionRowViewModel row, int digit)
    {
        if (_todos?.FirstOrDefault(t => string.Equals(t.Id, row.Id, StringComparison.Ordinal)) is not { Conflict: { } conflict } todo)
        {
            return;
        }

        var sent = await _api.KeepTodo(url, row.Id, digit == 1 ? "sindri" : "obsidian", CancellationToken.None).ConfigureAwait(true);
        StaleReads();
        if (sent.IsError)
        {
            await ConflictRefused(url, row, sent.Error).ConfigureAwait(true);
            return;
        }

        var kept = digit == 1 ? conflict.Sindri : conflict.Obsidian;
        _openId = null;
        Put(row.Id, sent.Value.Todo is { Conflict: null } served ? served : Kept(todo, kept));   // the todo as Sindri answers it, else as the pick leaves it
        _queueChangedAt = DateTimeOffset.MinValue;   // the owner's own change
        Picked?.Invoke(this, new TodoPickedEventArgs(KeptToast(kept, digit), () => UndoConflict(url, row.Id), Window(sent.Value.UndoUntil)));
    }

    // -409: the note's line moved since the row was drawn, so it is read again and the row says so. Anything else keeps the row with the reason.
    private async Task ConflictRefused(Uri url, AttentionRowViewModel row, Error error)
    {
        var lineMoved = string.Equals(error.Code, "SND-TODO-409", StringComparison.Ordinal);
        if (lineMoved || string.Equals(error.Code, "SND-TODO-404", StringComparison.Ordinal))
        {
            await Refresh(url, CancellationToken.None).ConfigureAwait(true);   // -404: the todo is gone, so the re-read drops the row
        }

        if (Attention is { } now && string.Equals(now.Id, row.Id, StringComparison.Ordinal))
        {
            now.Notice = lineMoved ? Labels.TodoAttnLineChanged : Labels.TodoCouldntSave(Refusal(error));
        }
    }

    private static string KeptToast(TodoSideView kept, int digit) =>
        kept.Removed ? (digit == 1 ? Labels.TodoAttnKeptRemoved : Labels.TodoAttnRemovedIt) : (digit == 1 ? Labels.TodoAttnKeptSindri : Labels.TodoAttnKeptObsidian);

    private async Task PickHold(Uri url, AttentionRowViewModel row, int digit)
    {
        var sent = await (digit == 1 ? _api.RestoreHold(url, row.NoteHash, CancellationToken.None) : _api.ApplyHold(url, row.NoteHash, CancellationToken.None)).ConfigureAwait(true);
        StaleReads();
        if (sent.IsError)
        {
            await HoldRefused(url, row, sent.Error).ConfigureAwait(true);
            return;
        }

        _openId = null;
        _note = _note is null ? null : _note with { Hold = null };
        Show();
        _queueChangedAt = DateTimeOffset.MinValue;
        var toast = digit == 1 ? Labels.TodoAttnPuttingBack(row.HeldCount) : Labels.TodoAttnApplied(row.HeldCount);
        Picked?.Invoke(this, new TodoPickedEventArgs(toast, () => UndoHold(url), Window(sent.Value.UndoUntil)));
    }

    // -409: the note changed since the row was drawn, so the row is read again and says the new count. -005: nothing is held any more. Anything else keeps the row with the reason.
    private async Task HoldRefused(Uri url, AttentionRowViewModel row, Error error)
    {
        var code = error.Code;
        if (string.Equals(code, "SND-TODO-409", StringComparison.Ordinal) || string.Equals(code, "SND-TODO-005", StringComparison.Ordinal))
        {
            await Refresh(url, CancellationToken.None).ConfigureAwait(true);
            if (_queue.FirstOrDefault(r => r.IsHold) is { } hold)
            {
                hold.Notice = string.Equals(code, "SND-TODO-409", StringComparison.Ordinal) ? Labels.TodoAttnHoldChangedAgain(hold.HeldCount) : Labels.TodoAttnNoHold;
            }

            return;
        }

        row.Notice = Labels.TodoCouldntSave(Refusal(error));
    }

    // Undo of a conflict pick: the conflict is open again from its stored versions, and the next read shows it. null when it worked, else why not.
    private async Task<string?> UndoConflict(Uri url, string id)
    {
        var sent = await _api.UndoKeepTodo(url, id, CancellationToken.None).ConfigureAwait(true);
        StaleReads();
        return await Reopened(url, sent.IsError ? sent.Error : null, id).ConfigureAwait(true);
    }

    private async Task<string?> UndoHold(Uri url)
    {
        var sent = await _api.UndoHold(url, CancellationToken.None).ConfigureAwait(true);
        StaleReads();
        return await Reopened(url, sent.IsError ? sent.Error : null, AttentionRowViewModel.HoldId).ConfigureAwait(true);
    }

    // The window the toast counts: until Sindri's undoUntil when that is within 10 s of now, else the full 10 s (the two clocks may differ).
    private int? Window(DateTimeOffset? until)
    {
        var seconds = until is { } at ? (int)Math.Ceiling((at - _clock.GetUtcNow()).TotalSeconds) : 0;
        return seconds is > 0 and <= 10 ? seconds : null;
    }

    private async Task<string?> Reopened(Uri url, Error? error, string id)
    {
        if (error is { } refused)
        {
            await Refresh(url, CancellationToken.None).ConfigureAwait(true);
            return string.Equals(refused.Code, "SND-TODO-409", StringComparison.Ordinal) ? Labels.TodoUndoChanged
                : string.Equals(refused.Code, "SND-TODO-005", StringComparison.Ordinal) ? Labels.TodoAttnNothingToUndo
                : Refusal(refused);
        }

        _shownId = id;
        await Refresh(url, CancellationToken.None).ConfigureAwait(true);
        _queueChangedAt = DateTimeOffset.MinValue;   // the owner's own change
        return null;
    }

    // The todo as the picked version has it (a removal removes it); the conflict is closed.
    private TodoView Kept(TodoView todo, TodoSideView side) => todo with
    {
        Conflict = null,
        Text = side.Text ?? todo.Text,
        Due = side.Removed ? todo.Due : side.Due,
        DoneAt = side.Done && !side.Removed ? todo.DoneAt ?? _clock.GetUtcNow() : null,
        Removed = side.Removed,
        RemovedAt = side.Removed ? _clock.GetUtcNow() : null,
    };
}
