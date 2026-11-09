using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>What a pass reads from the ledger and the views (todo spec §4.5 The base, First sync and import).</summary>
/// <param name="d">What a pass reaches.</param>
internal sealed class SyncLedger(SyncDeps d)
{
    private const int Page = 500;

    /// <summary>The newest 'todo.note.synced'.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The mark, or null for none.</returns>
    public async ValueTask<Result<SyncedMark?>> LastSynced(CancellationToken cancellationToken)
    {
        var latest = await d.Ledger.Latest(EventTypes.TodoNoteSynced, cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value is not { } recorded)
        {
            return latest.IsError ? latest.Error : Results.Success<SyncedMark?>(value: null);
        }

        var payload = d.Events.Payload<TodoNoteSynced>(recorded);
        return payload.IsError ? payload.Error : new SyncedMark(payload.Value, recorded.Sequence);
    }

    /// <summary>Whether the newest 'todo.note.imported' names this note.</summary>
    /// <param name="place">The note.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the owner has imported it.</returns>
    public async ValueTask<Result<bool>> Imported(NotePlace place, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(place);
        var latest = await d.Ledger.Latest(EventTypes.TodoNoteImported, cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value is not { } recorded)
        {
            return latest.IsError ? latest.Error : false;
        }

        var payload = d.Events.Payload<TodoNoteImported>(recorded);
        return payload.IsError ? payload.Error : string.Equals(payload.Value.Vault, place.Vault, StringComparison.Ordinal) && string.Equals(payload.Value.Note, place.Note, StringComparison.Ordinal);
    }

    /// <summary>Every todo row.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows.</returns>
    public async ValueTask<Result<IReadOnlyList<TodoRow>>> Rows(CancellationToken cancellationToken) =>
        await d.Views.List<TodoRow>(ViewNames.Todo, cancellationToken).ConfigureAwait(false);

    /// <summary>The conflict picks recorded after <paramref name="sequence"/>, and a "Put them back" of the hold; an Undo (a reopened conflict or hold) cancels its pick.</summary>
    /// <param name="sequence">The last sync's sequence.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The picks.</returns>
    public async ValueTask<Result<PickedLines>> Picked(long sequence, CancellationToken cancellationToken)
    {
        var bases = new Dictionary<TodoId, string>();
        var removed = new HashSet<TodoId>();
        string? restored = null;
        var cursor = sequence;
        while (true)
        {
            var page = await d.Ledger.ReadAfter(cursor, Page, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var recorded in page.Value)
            {
                cursor = recorded.Sequence;
                var seen = Fold(recorded, bases, removed, ref restored);
                if (seen.IsError)
                {
                    return seen.Error;
                }
            }

            if (page.Value.Count < Page)
            {
                return await Restore(new PickedLines(bases, removed), restored, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // a "Put them back" is a pick for Sindri on every change of the hold it closed: a removal's line comes back, an edit or un-tick is written over from Sindri
    private async ValueTask<Result<PickedLines>> Restore(PickedLines picked, string? noteHash, CancellationToken cancellationToken)
    {
        if (noteHash is null)
        {
            return picked;
        }

        var latest = await d.Ledger.Latest(EventTypes.TodoNoteHeld, cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value is not { } recorded)
        {
            return latest.IsError ? latest.Error : picked;
        }

        var held = d.Events.Payload<TodoNoteHeld>(recorded);
        if (held.IsError)
        {
            return held.Error;
        }

        if (!string.Equals(held.Value.NoteHash, noteHash, StringComparison.Ordinal))
        {
            return picked;
        }

        var bases = new Dictionary<TodoId, string>(picked.Bases);
        var removed = new HashSet<TodoId>(picked.Removed);
        foreach (var change in held.Value.Changes)
        {
            _ = change.NoteLine is { } line ? bases.TryAdd(change.Id, line) : removed.Add(change.Id);
        }

        return new PickedLines(bases, removed) { Held = [.. held.Value.Changes.Select(static c => c.Id)] };
    }

    private Result<Unit> Fold(Core.Ledger.LedgerEvent recorded, Dictionary<TodoId, string> bases, HashSet<TodoId> removed, ref string? restored)
    {
        var type = recorded.Envelope.Type;
        if (string.Equals(type, EventTypes.TodoNoteHeldResolved, StringComparison.Ordinal))
        {
            var closed = d.Events.Payload<TodoNoteHeldResolved>(recorded);
            restored = closed.IsSuccess && string.Equals(closed.Value.Kept, "restored", StringComparison.Ordinal) ? closed.Value.NoteHash : null;
            return closed.IsError ? closed.Error : Unit.Value;
        }

        if (string.Equals(type, EventTypes.TodoNoteHeldReopened, StringComparison.Ordinal))
        {
            restored = null;
            return Unit.Value;
        }

        return FoldConflict(recorded, bases, removed);
    }

    private Result<Unit> FoldConflict(Core.Ledger.LedgerEvent recorded, Dictionary<TodoId, string> bases, HashSet<TodoId> removed)
    {
        if (string.Equals(recorded.Envelope.Type, EventTypes.TodoConflictResolved, StringComparison.Ordinal))
        {
            var picked = d.Events.Payload<TodoConflictResolved>(recorded);
            if (picked.IsError)
            {
                return picked.Error;
            }

            _ = bases.Remove(picked.Value.Id);
            _ = removed.Remove(picked.Value.Id);
            _ = picked.Value.BaseLine.Length == 0 ? removed.Add(picked.Value.Id) : bases.TryAdd(picked.Value.Id, picked.Value.BaseLine);
        }
        else if (string.Equals(recorded.Envelope.Type, EventTypes.TodoConflictReopened, StringComparison.Ordinal))
        {
            var undone = d.Events.Payload<TodoConflictReopened>(recorded);
            if (undone.IsError)
            {
                return undone.Error;
            }

            _ = bases.Remove(undone.Value.Id);
            _ = removed.Remove(undone.Value.Id);
        }

        return Unit.Value;
    }
}
