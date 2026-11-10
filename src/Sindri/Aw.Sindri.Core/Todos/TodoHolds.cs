using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Core.Todos;

/// <summary>
/// Which hold is open, read from the ledger so it survives a restart (todo spec §4.1): the newest 'todo.note.held' or '.held.reopened' opens it, a newer '.held.resolved' closes it. The changes come from the newest
/// 'todo.note.held', whose pick a reopen undid.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="events">Reads the payloads.</param>
public sealed class TodoHolds(ILedgerStore ledger, EventFactory events)
{
    /// <summary>The open hold.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The hold, or null when none is open.</returns>
    public async ValueTask<Result<TodoOpenHold?>> Open(CancellationToken cancellationToken)
    {
        var held = await Newest(EventTypes.TodoNoteHeld, cancellationToken).ConfigureAwait(false);
        var reopened = await Newest(EventTypes.TodoNoteHeldReopened, cancellationToken).ConfigureAwait(false);
        var resolved = await Newest(EventTypes.TodoNoteHeldResolved, cancellationToken).ConfigureAwait(false);
        var failed = new[] { held, reopened, resolved }.FirstOrDefault(static r => r.IsError);
        if (failed is not null)
        {
            return failed.Error;
        }

        var opened = Math.Max(Sequence(held), Sequence(reopened));
        return held.Value is { } latest && opened >= Sequence(resolved) ? Read(latest, opened) : Results.Success<TodoOpenHold?>(value: null);
    }

    private static long Sequence(Result<LedgerEvent?> latest) => latest.Value?.Sequence ?? 0;

    private async ValueTask<Result<LedgerEvent?>> Newest(string type, CancellationToken cancellationToken) => await ledger.Latest(type, cancellationToken).ConfigureAwait(false);

    private Result<TodoOpenHold?> Read(LedgerEvent latest, long opened)
    {
        var payload = events.Payload<TodoNoteHeld>(latest);
        return payload.IsError
            ? payload.Error
            : Results.Success<TodoOpenHold?>(new TodoOpenHold([.. payload.Value.Changes.Select(Held).OfType<HeldChange>()], payload.Value.NoteHash, opened, payload.Value.OpenInBase));
    }

    private static HeldChange? Held(TodoHeldChange change) => change.Kind switch
    {
        "removed" => new HeldChange(change.Id, HeldKind.Removed, change.NoteLine),
        "edited" => new HeldChange(change.Id, HeldKind.Edited, change.NoteLine),
        "reopened" => new HeldChange(change.Id, HeldKind.Reopened, change.NoteLine),
        _ => null,
    };
}
