using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Todos;

/// <summary>Folds the nine todo events into the 'todo' view (todo spec §4.2): a row per todo, a pure fold a 'views rebuild' reproduces. An event for a todo the view doesn't hold is skipped.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class TodosProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.Todo];

    public bool Handles(string eventType) =>
        eventType.StartsWith("todo.", StringComparison.Ordinal) && !eventType.StartsWith("todo.note.", StringComparison.Ordinal);

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        var type = recorded.Envelope.Type;
        return type switch
        {
            EventTypes.TodoAdded => Added(recorded, rows, cancellationToken),
            EventTypes.TodoChanged => Edit<TodoChanged>(recorded, rows, static p => p.Id, (row, p) => Moved(row, p.Version, p.By, p.Via, recorded) with
            {
                Text = p.Text ?? row.Text,
                Due = p.ClearDue ? null : p.Due ?? row.Due,
                Links = p.Links ?? row.Links,
            }, cancellationToken),
            EventTypes.TodoDone => Edit<TodoDone>(recorded, rows, static p => p.Id, (row, p) => Moved(row, p.Version, p.By, p.Via, recorded) with { DoneAt = p.DoneAt ?? recorded.Envelope.OccurredAt }, cancellationToken),
            EventTypes.TodoReopened => Edit<TodoReopened>(recorded, rows, static p => p.Id, (row, p) => Moved(row, p.Version, p.By, p.Via, recorded) with { DoneAt = null }, cancellationToken),
            EventTypes.TodoRemoved => Edit<TodoRemoved>(recorded, rows, static p => p.Id, (row, p) => Moved(row, p.Version, p.By, p.Via, recorded) with
            {
                Removed = true,
                RemovedAt = recorded.Envelope.OccurredAt,
                RemovedBy = p.By,
            }, cancellationToken),
            EventTypes.TodoRestored => Edit<TodoRestored>(recorded, rows, static p => p.Id, (row, p) => Moved(row, p.Version, p.By, p.Via, recorded) with
            {
                Removed = false,
                RemovedAt = null,
                RemovedBy = null,
            }, cancellationToken),
            _ => Conflicts(recorded, rows, cancellationToken),
        };
    }

    // a conflict event moves no version: the todo is the same todo, with a question open on it
    private ValueTask<Result<Unit>> Conflicts(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken) => recorded.Envelope.Type switch
    {
        EventTypes.TodoConflicted => Edit<TodoConflicted>(recorded, rows, static p => p.Id, (row, p) =>
            row with { Conflict = new TodoOpenConflict(p.Sindri, p.Obsidian, p.NoteHash), Sequence = recorded.Sequence }, cancellationToken),
        EventTypes.TodoConflictResolved => Edit<TodoConflictResolved>(recorded, rows, static p => p.Id, (row, _) =>
            row with { Conflict = null, PickedConflict = row.Conflict ?? row.PickedConflict, Sequence = recorded.Sequence }, cancellationToken),
        EventTypes.TodoConflictReopened => Edit<TodoConflictReopened>(recorded, rows, static p => p.Id, (row, _) =>
            row with { Conflict = row.PickedConflict ?? row.Conflict, PickedConflict = null, Sequence = recorded.Sequence }, cancellationToken),
        _ => ValueTask.FromResult<Result<Unit>>(Unit.Value),
    };

    private static TodoRow Moved(TodoRow row, long version, string by, string? via, LedgerEvent recorded) =>
        row with { Version = version, LastBy = by, LastVia = via, Sequence = recorded.Sequence };

    private async ValueTask<Result<Unit>> Added(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var added = events.Payload<TodoAdded>(recorded);
        if (added.IsError)
        {
            return added.Error;
        }

        var p = added.Value;
        var row = new TodoRow(
            p.Id, p.Text, p.Due, p.Links, recorded.Envelope.OccurredAt, p.By, p.Via, DoneAt: null, Removed: false, RemovedAt: null, RemovedBy: null,
            LastLineHash: null, Version: 1, p.By, p.Via, Conflict: null, PickedConflict: null, recorded.Sequence);
        return await rows.Put(ViewNames.Todo, p.Id.Value, row, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Edit<TPayload>(LedgerEvent recorded, IViewRows rows, Func<TPayload, TodoId> id, Func<TodoRow, TPayload, TodoRow> fold, CancellationToken cancellationToken)
    {
        var payload = events.Payload<TPayload>(recorded);
        if (payload.IsError)
        {
            return payload.Error;
        }

        var key = id(payload.Value).Value;
        var row = await rows.Get<TodoRow>(ViewNames.Todo, key, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        return row.Value is null
            ? Unit.Value
            : await rows.Put(ViewNames.Todo, key, fold(row.Value, payload.Value), cancellationToken).ConfigureAwait(false);
    }
}
