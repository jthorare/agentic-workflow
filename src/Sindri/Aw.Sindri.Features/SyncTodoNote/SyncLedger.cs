using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
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

    /// <summary>The conflict picks recorded after <paramref name="sequence"/>; an Undo (a reopened conflict) cancels its pick.</summary>
    /// <param name="sequence">The last sync's sequence.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The picks.</returns>
    public async ValueTask<Result<PickedLines>> Picked(long sequence, CancellationToken cancellationToken)
    {
        var bases = new Dictionary<TodoId, string>();
        var removed = new HashSet<TodoId>();
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
                var seen = Fold(recorded, bases, removed);
                if (seen.IsError)
                {
                    return seen.Error;
                }
            }

            if (page.Value.Count < Page)
            {
                return new PickedLines(bases, removed);
            }
        }
    }

    private Result<Unit> Fold(Core.Ledger.LedgerEvent recorded, Dictionary<TodoId, string> bases, HashSet<TodoId> removed)
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
