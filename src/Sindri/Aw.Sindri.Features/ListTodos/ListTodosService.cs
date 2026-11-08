using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListTodos;

/// <summary>The todos of one state, oldest first, with the counts of every state (todo spec §4.4): one read of the 'todo' view.</summary>
/// <param name="views">The todos.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class ListTodosService(IViewReader views, IProjectorStatus status, ICallerContextAccessor callers) : IService<ListTodosRequest, ListTodosResponse>
{
    public async ValueTask<Result<ListTodosResponse>> Handle(ListTodosRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the todos need a caller");
        }

        var rows = await views.List<TodoRow>(ViewNames.Todo, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var counts = new TodoCounts(rows.Value.Count(TodoDecisions.IsOpen), rows.Value.Count(r => !r.Removed && r.DoneAt is not null), rows.Value.Count(r => r.Removed));
        var shown = rows.Value.Where(r => request.State switch
        {
            TodoListState.Open => TodoDecisions.IsOpen(r),
            TodoListState.Done => !r.Removed && r.DoneAt is not null,
            TodoListState.All => true,
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.State, "not a todo list state"),
        });
        return new ListTodosResponse(status.Current.AsOf, request.State, counts, [.. shown.OrderBy(static r => r.AddedAt).ThenBy(static r => r.Id.Value, StringComparer.Ordinal).Select(static r => TodoItem.From(r))]);
    }
}
