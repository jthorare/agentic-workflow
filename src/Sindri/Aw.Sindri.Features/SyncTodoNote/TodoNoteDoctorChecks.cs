using System.Globalization;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>
/// Doctor's 'todo' row (todo spec §6): whether the Obsidian note syncs, and when it last did. Ok while it syncs or when no key is set; a warning for a note that can't be read, a disk that can't swap, a note that
/// waits for its import or stays busy, and after 3 failed passes in a row. The status is this process's own (reset by a restart); the last sync comes from the ledger.
/// </summary>
/// <param name="locator">Finds the note the profile names.</param>
/// <param name="sync">What this process's sync knows.</param>
/// <param name="ledger">Reads the last 'todo.note.synced'.</param>
internal sealed class TodoNoteDoctorChecks(INoteLocator locator, ITodoNoteSync sync, ILedgerReader ledger) : IDoctorChecks
{
    internal const string Name = "todo";

    private const int FailuresBeforeWarning = 3;
    private const string Fix = "check todo.obsidian: sindri todo note set <vault> <note>";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var located = await locator.Locate(cancellationToken).ConfigureAwait(false);
        if (located.IsError)
        {
            return [new DoctorCheck(Name, CheckState.Warn, $"{located.Error.Code}: {located.Error.Message}", Fix)];
        }

        if (located.Value is not { } place)
        {
            return [new DoctorCheck(Name, CheckState.Ok, "note: not set (no todo.obsidian key)", Fix: null)];
        }

        var synced = await ledger.Latest(EventTypes.TodoNoteSynced, cancellationToken).ConfigureAwait(false);
        var last = synced.IsSuccess && synced.Value is { } sevent
            ? sevent.Envelope.OccurredAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm'Z'", CultureInfo.InvariantCulture)
            : "none yet";
        return [Judge(place, sync.Status, last)];
    }

    private static DoctorCheck Judge(NotePlace place, TodoNoteStatus status, string last)
    {
        var where = $"{place.Vault}/{place.Note}";
        if (status.FailedInARow >= FailuresBeforeWarning)
        {
            return new DoctorCheck(Name, CheckState.Warn, string.Create(CultureInfo.InvariantCulture, $"note: failing {status.FailedInARow} in a row ({status.LastError}); {where}; last sync: {last}"), "fix the note or its folder; the next check is within 60 s");
        }

        return status.State switch
        {
            TodoNoteState.WaitingForImport => new DoctorCheck(Name, CheckState.Warn, $"note: waiting for import; {where}", "sindri todo note import --dry-run, then sindri todo note import --expect N"),
            TodoNoteState.CantRead => new DoctorCheck(Name, CheckState.Warn, $"note: can't read ({status.LastError}); {where}; last sync: {last}", "fix the note or its folder; the next check is within 60 s"),
            TodoNoteState.OneWay => new DoctorCheck(Name, CheckState.Warn, string.Create(CultureInfo.InvariantCulture, $"note: one way: no atomic swap ({status.NotAdopted} bare lines not adopted); {where}; last sync: {last}"), "put the vault on a disk that supports an atomic swap"),
            TodoNoteState.Busy => new DoctorCheck(Name, CheckState.Warn, $"note: busy, trying again; {where}; last sync: {last}", Fix: null),
            TodoNoteState.NotSet or TodoNoteState.Syncing => new DoctorCheck(Name, CheckState.Ok, $"note: syncing; {where}; last sync: {last}", Fix: null),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status.State, "not a note state"),
        };
    }
}
