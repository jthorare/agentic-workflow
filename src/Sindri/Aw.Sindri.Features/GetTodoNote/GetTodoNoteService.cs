using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetTodoNote;

/// <summary>The Obsidian todo note's state (todo spec §4.4): the profile's key, the sync's in-memory status, the last 'todo.note.synced' and the todos with an open conflict.</summary>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
/// <param name="note">What this process's sync knows.</param>
/// <param name="locator">Finds the note the profile names.</param>
/// <param name="ledger">Reads the last sync.</param>
/// <param name="views">The todos.</param>
/// <param name="reads">The hold, the picks and the lines that can't sync.</param>
internal sealed class GetTodoNoteService(IProjectorStatus status, ICallerContextAccessor callers, ITodoNoteSync note, INoteLocator locator, ILedgerReader ledger, IViewReader views, TodoNoteReads reads) : IService<GetTodoNoteRequest, GetTodoNoteResponse>
{
    public async ValueTask<Result<GetTodoNoteResponse>> Handle(GetTodoNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the todo note needs a caller");
        }

        var located = await locator.Locate(cancellationToken).ConfigureAwait(false);
        var asOf = status.Current.AsOf;
        var current = note.Status;
        if (Unready(asOf, located, current) is { } early)
        {
            return early;
        }

        var rows = await views.List<TodoRow>(ViewNames.Todo, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var synced = await ledger.Latest(EventTypes.TodoNoteSynced, cancellationToken).ConfigureAwait(false);
        var conflicts = rows.Value.Where(static r => r.Conflict is not null).OrderBy(static r => r.Sequence).Select(static r => r.Id.Value).ToList();
        var hold = await reads.Hold(rows.Value, cancellationToken).ConfigureAwait(false);
        if (hold.IsError)
        {
            return hold.Error;
        }

        var cantSync = await reads.CantSync(cancellationToken).ConfigureAwait(false);
        var answer = Answer(asOf, located, current, synced.IsSuccess ? synced.Value?.Envelope.OccurredAt : null, conflicts);
        return answer with { Hold = hold.Value, Picks = reads.Picks(), CantSync = cantSync };
    }

    // The two answers that need no read of the todos: no profile file yet, and a profile with no note set.
    private GetTodoNoteResponse? Unready(AsOf asOf, Result<NotePlace?> located, TodoNoteStatus current)
    {
        if (located.IsError && string.Equals(located.Error.Code, SindriErrors.ProfileMissing.Code, StringComparison.Ordinal) && !reads.ProfileFileExists())
        {
            return new GetTodoNoteResponse(asOf, TodoNoteWords.Of(TodoNoteState.NotSetUp), Vault: null, Note: null, current.LastCheckAt, LastSyncAt: null, LastError: null, [], Hold: null, [], []);   // no profile yet (#3113): not a failed read
        }

        if (located.IsSuccess && located.Value is null)
        {
            return new GetTodoNoteResponse(asOf, TodoNoteWords.Of(TodoNoteState.NotSet), Vault: null, Note: null, current.LastCheckAt, LastSyncAt: null, LastError: null, [], Hold: null, [], []);
        }

        return null;
    }

    private static GetTodoNoteResponse Answer(AsOf asOf, Result<NotePlace?> located, TodoNoteStatus current, DateTimeOffset? lastSync, List<string> conflicts)
    {
        var place = located.IsSuccess ? located.Value : null;
        var state = located.IsError ? TodoNoteState.CantRead : current.State == TodoNoteState.NotSet ? TodoNoteState.Syncing : current.State;   // a key with no pass yet in this process is about to sync
        var error = located.IsError ? located.Error.Message : current.LastError;
        return new GetTodoNoteResponse(asOf, TodoNoteWords.Of(state), place?.Vault, place?.Note, current.LastCheckAt, lastSync, error, conflicts, Hold: null, [], []);
    }
}
