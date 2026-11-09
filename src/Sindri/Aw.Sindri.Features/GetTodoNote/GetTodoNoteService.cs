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
internal sealed class GetTodoNoteService(IProjectorStatus status, ICallerContextAccessor callers, ITodoNoteSync note, INoteLocator locator, ILedgerReader ledger, IViewReader views) : IService<GetTodoNoteRequest, GetTodoNoteResponse>
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
        if (located.IsSuccess && located.Value is null)
        {
            return new GetTodoNoteResponse(asOf, TodoNoteWords.Of(TodoNoteState.NotSet), Vault: null, Note: null, current.LastCheckAt, LastSyncAt: null, LastError: null, [], [], []);
        }

        var rows = await views.List<TodoRow>(ViewNames.Todo, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var synced = await ledger.Latest(EventTypes.TodoNoteSynced, cancellationToken).ConfigureAwait(false);
        var conflicts = rows.Value.Where(static r => r.Conflict is not null).OrderBy(static r => r.Sequence).Select(static r => r.Id.Value).ToList();
        return Answer(asOf, located, current, synced.IsSuccess ? synced.Value?.Envelope.OccurredAt : null, conflicts);
    }

    private static GetTodoNoteResponse Answer(AsOf asOf, Result<NotePlace?> located, TodoNoteStatus current, DateTimeOffset? lastSync, List<string> conflicts)
    {
        var place = located.IsSuccess ? located.Value : null;
        var state = located.IsError ? TodoNoteState.CantRead : current.State == TodoNoteState.NotSet ? TodoNoteState.Syncing : current.State;   // a key with no pass yet in this process is about to sync
        var error = located.IsError ? located.Error.Message : current.LastError;
        return new GetTodoNoteResponse(asOf, TodoNoteWords.Of(state), place?.Vault, place?.Note, current.LastCheckAt, lastSync, error, conflicts, [], []);
    }
}
