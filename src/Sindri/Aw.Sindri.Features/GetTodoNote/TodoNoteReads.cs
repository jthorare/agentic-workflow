using Aw.Kernel;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.GetTodoNote;

/// <summary>What the note read adds to the sync status (todo spec §4.4): the open hold, the picks inside their Undo window and the note lines that can't sync.</summary>
/// <param name="picks">The picks still in their Undo window.</param>
/// <param name="holds">The open hold.</param>
/// <param name="source">Reads the note.</param>
/// <param name="ledger">Reads the hold's opening event.</param>
/// <param name="clock">Sindri's clock.</param>
/// <param name="profileFile">Says whether the profile file is on disk, to tell a new home from a file it can't read; null counts it absent.</param>
internal sealed class TodoNoteReads(TodoPickWindow picks, TodoHolds holds, ITodoNoteSource source, ILedgerReader ledger, TimeProvider clock, IProfileFile? profileFile = null)
{
    /// <summary>Whether the profile file is on disk (readable or not).</summary>
    internal bool ProfileFileExists() => profileFile?.Exists() == true;

    internal async ValueTask<Result<TodoNoteHold?>> Hold(IReadOnlyList<TodoRow> rows, CancellationToken cancellationToken)
    {
        var open = await holds.Open(cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        if (open.Value is not { } hold)
        {
            return Results.Success<TodoNoteHold?>(value: null);
        }

        // why: the event at the hold's sequence is the 'todo.note.held' or '.held.reopened' that opened it, so its time is when the hold opened
        var opened = await ledger.ReadAfter(hold.Sequence - 1, 1, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        var heldAt = opened.Value.FirstOrDefault(e => e.Sequence == hold.Sequence)?.Envelope.OccurredAt ?? clock.GetUtcNow();
        var byId = rows.ToDictionary(static r => r.Id);
        var changes = hold.Changes.Select(c => new TodoNoteHeldChange(c.Id, KindWord(c.Kind), byId.TryGetValue(c.Id, out var row) ? row.Text : string.Empty, c.NoteLine)).ToList();
        // why: the base the 10% rule counted against is on the event; a 'todo.note.held' written before that field existed has none, so those
        // holds still show Sindri's open todos now (the old approximation) rather than a made-up number
        var openInBase = hold.OpenInBase ?? rows.Count(static r => !r.Removed && r.DoneAt is null);
        return new TodoNoteHold(hold.NoteHash, heldAt, openInBase, changes);
    }

    private static string KindWord(HeldKind kind) => kind switch
    {
        HeldKind.Removed => "removed",
        HeldKind.Edited => "edited",
        HeldKind.Reopened => "reopened",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "not a held kind"),
    };

    internal List<TodoNotePick> Picks()
    {
        var now = clock.GetUtcNow();
        var (todos, holdOpen) = picks.Open(now);
        var list = new List<TodoNotePick>();
        foreach (var id in todos)
        {
            if (picks.Find(id, now) is { } pick)
            {
                list.Add(new TodoNotePick("conflict", id, pick.Kept, pick.By, TodoPickWindow.Until(pick.At)));
            }
        }

        if (holdOpen && picks.FindHold(now) is { } hold)
        {
            list.Add(new TodoNotePick("hold", Id: null, hold.Kept, hold.By, TodoPickWindow.Until(hold.At)));
        }

        return list;
    }

    internal async ValueTask<IReadOnlyList<TodoNoteCantSync>> CantSync(CancellationToken cancellationToken)
    {
        var read = await source.Read(cancellationToken).ConfigureAwait(false);
        return read.IsSuccess && read.Value is { } snapshot ? [.. NoteBrokenLines.Of(snapshot.Document).Select(static b => new TodoNoteCantSync(b.Line, b.Reason))] : [];
    }
}
