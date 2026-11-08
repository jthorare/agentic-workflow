using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.MarkTodo;

/// <summary>
/// Checks off, reopens, removes or restores a todo (todo spec §4.1, §4.4, D8, D9; request class): reads its row, refuses an unknown todo, an open conflict, a stale version (when the caller sent one) and a verb the
/// state doesn't fit, and appends the verb's event keyed by the todo and the version it moves to.
/// </summary>
/// <param name="views">The todos.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller.</param>
internal sealed class MarkTodoService(IViewReader views, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers)
    : IService<MarkTodoRequest, TodoAccepted>
{
    public async ValueTask<Result<TodoAccepted>> Handle(MarkTodoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var begun = TodoWrite.Begin(callers, held, "MarkTodo");
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

        var fits = TodoDecisions.Fits(row.Value, request.Verb);
        if (fits.IsError)
        {
            return fits.Error;
        }

        var version = row.Value.Version + 1;
        var appended = await TodoWrite.Append(ledger, grant, Entry(request, version, TodoWrite.By(caller)), caller.CommandId, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new TodoAccepted(appended.Value, request.Id, version, row.Value.Due);
    }

    private LedgerEntry Entry(MarkTodoRequest request, long version, string by)
    {
        var via = TodoWrite.Via(request.Piped);
        return request.Verb switch
        {
            TodoVerb.Done => events.Entry(EventTypes.TodoDone, new TodoDone(request.Id, version, by, via), Key(EventTypes.TodoDone, request.Id, version)),
            TodoVerb.Reopen => events.Entry(EventTypes.TodoReopened, new TodoReopened(request.Id, version, by, via), Key(EventTypes.TodoReopened, request.Id, version)),
            TodoVerb.Remove => events.Entry(EventTypes.TodoRemoved, new TodoRemoved(request.Id, version, by, via), Key(EventTypes.TodoRemoved, request.Id, version)),
            TodoVerb.Restore => events.Entry(EventTypes.TodoRestored, new TodoRestored(request.Id, version, by, via), Key(EventTypes.TodoRestored, request.Id, version)),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Verb, "not a todo verb"),
        };
    }

    private static string Key(string type, TodoId id, long version) => string.Create(CultureInfo.InvariantCulture, $"{type}:{id.Value}:v{version}");
}
