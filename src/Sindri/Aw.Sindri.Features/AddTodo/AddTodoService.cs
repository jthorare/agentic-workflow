using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.AddTodo;

/// <summary>
/// Adds a todo (todo spec §4.1, §4.4; request class): checks and scrubs the text and links, then inside one lock per actor lets <see cref="TodoAdds"/> clear it (a retried client id returns the todo it made and doesn't count,
/// then the 500 open todos cap and the hourly limit), and appends 'todo.added' keyed by the client id (else by the new todo id).
/// </summary>
/// <param name="adds">What an add must clear.</param>
/// <param name="input">Checks and scrubs the fields.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="ids">The id source for the new todo.</param>
/// <param name="callers">The caller.</param>
internal sealed class AddTodoService(TodoAdds adds, TodoInput input, ILedgerStore ledger, HeldLease held, EventFactory events, IIdGenerator ids, ICallerContextAccessor callers)
    : IService<AddTodoRequest, TodoAccepted>
{
    public async ValueTask<Result<TodoAccepted>> Handle(AddTodoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var begun = TodoWrite.Begin(callers, held, "AddTodo");
        if (begun.IsError)
        {
            return begun.Error;
        }

        var fields = await input.Edit(request.Text, request.Links ?? [], cancellationToken).ConfigureAwait(false);
        if (fields.IsError)
        {
            return fields.Error;
        }

        var (caller, grant) = begun.Value;
        var by = TodoWrite.By(caller);
        var turn = adds.Lock(by);
        await turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var admitted = await adds.Admit(request.ClientId, caller, by, request.Piped, cancellationToken).ConfigureAwait(false);
            if (admitted.IsError)
            {
                return admitted.Error;
            }

            return admitted.Value ?? await Append(request, fields.Value, (caller, grant, by), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = turn.Release();
        }
    }

    private async ValueTask<Result<TodoAccepted>> Append(AddTodoRequest request, (string? Text, IReadOnlyList<string>? Links) fields, (CallerContext Caller, LeaseGrant Grant, string By) who, CancellationToken cancellationToken)
    {
        var id = TodoId.New(ids);
        var added = new TodoAdded(id, fields.Text ?? request.Text, request.Due, fields.Links ?? [], request.ClientId, who.By, TodoWrite.Via(request.Piped));
        var key = request.ClientId is { } named ? $"{EventTypes.TodoAdded}:client:{named.Value}" : $"{EventTypes.TodoAdded}:{id.Value}";
        var appended = await TodoWrite.Append(ledger, who.Grant, events.Entry(EventTypes.TodoAdded, added, key), who.Caller.CommandId, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new TodoAccepted(appended.Value, id, 1, request.Due);
    }
}
