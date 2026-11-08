using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ChangeTodo;

/// <summary>
/// Edits a todo (todo spec §4.1, §4.4, D9; request class): reads its row, refuses an unknown todo, an open conflict and a stale version, checks and scrubs the new text and links, and appends
/// 'todo.changed' under the todo's version key (TodoWrite.VersionKey, shared by every state change). Losing that key to another command is SND-TODO-409.
/// </summary>
/// <param name="views">The todos.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="input">Checks and scrubs the text and links.</param>
/// <param name="callers">The caller.</param>
internal sealed class ChangeTodoService(IViewReader views, ILedgerStore ledger, HeldLease held, EventFactory events, TodoInput input, ICallerContextAccessor callers)
    : IService<ChangeTodoRequest, TodoAccepted>
{
    public async ValueTask<Result<TodoAccepted>> Handle(ChangeTodoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var begun = TodoWrite.Begin(callers, held, "ChangeTodo");
        if (begun.IsError)
        {
            return begun.Error;
        }

        var (caller, grant) = begun.Value;
        var read = await views.Get<TodoRow>(ViewNames.Todo, request.Id.Value, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var row = TodoDecisions.Writable(request.Id, read.Value, request.Version);
        if (row.IsError)
        {
            return row.Error;
        }

        if (TodoDecisions.NoEdit(request.Text, request.Due, request.ClearDue, request.Links) is { } empty)
        {
            return empty;
        }

        var fields = await input.Edit(request.Text, request.Links, cancellationToken).ConfigureAwait(false);
        if (fields.IsError)
        {
            return fields.Error;
        }

        var version = row.Value.Version + 1;
        var changed = new TodoChanged(request.Id, version, fields.Value.Text, request.Due, request.ClearDue, fields.Value.Links, TodoWrite.By(caller), TodoWrite.Via(request.Piped));
        var key = TodoWrite.VersionKey(request.Id, version);
        var appended = await TodoWrite.Append(ledger, grant, events.Entry(EventTypes.TodoChanged, changed, key), caller.CommandId, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new TodoAccepted(appended.Value, request.Id, version, request.ClearDue ? null : request.Due ?? row.Value.Due);
    }
}
