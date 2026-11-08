using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.AddTodo;
using Aw.Sindri.Features.ChangeTodo;
using Aw.Sindri.Features.CheckTodoNote;
using Aw.Sindri.Features.GetTodo;
using Aw.Sindri.Features.GetTodoNote;
using Aw.Sindri.Features.ListTodos;
using Aw.Sindri.Features.MarkTodo;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The todo routes (todo spec §4.4): GET /sindri/todos?state=, /todos/note and /todos/{todoId}; POST /sindri/todos, /todos/{todoId}/change and /done, /reopen, /remove, /restore, and /todos/note/check (writes nothing). Each binds, calls one service and maps the result.
/// A session caller's answers, and any caller's that sends fence=1, have the todo text inside the untrusted fence. A write that came with fence=1 records 'via: pipe'.
/// </summary>
internal static class TodoEndpoints
{
    internal static RouteGroupBuilder MapTodoReads(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/todos", static async Task<IResult> (string? state, string? fence, IService<ListTodosRequest, ListTodosResponse> service, ICallerContextAccessor callers, ITodoFencing fencing, CancellationToken ct) =>
        {
            var which = State(state);
            if (which.IsError)
            {
                return which.ToHttpResult();
            }

            var list = await service.Handle(new ListTodosRequest(which.Value), ct).ConfigureAwait(false);
            return list.IsError || !Fenced(callers, fence) ? list.ToHttpResult() : fencing.List(list.Value);
        });
        _ = group.MapGet("/todos/note", static async Task<IResult> (IService<GetTodoNoteRequest, GetTodoNoteResponse> service, CancellationToken ct)
            => (await service.Handle(new GetTodoNoteRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/todos/{todoId}", static async Task<IResult> (string todoId, string? fence, IService<GetTodoRequest, GetTodoResponse> service, ICallerContextAccessor callers, ITodoFencing fencing, CancellationToken ct) =>
        {
            var id = TodoWire.Id(todoId);
            if (id.IsError)
            {
                return id.ToHttpResult();
            }

            var one = await service.Handle(new GetTodoRequest(id.Value), ct).ConfigureAwait(false);
            return one.IsError || !Fenced(callers, fence) ? one.ToHttpResult() : fencing.One(one.Value);
        });
        return group;
    }

    internal static RouteGroupBuilder MapTodoWrites(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/todos", static async (AddTodoBody body, string? fence, IService<AddTodoRequest, TodoAccepted> service, CancellationToken ct) =>
        {
            var request = body.ToRequest(TodoWire.Piped(fence));
            return request.IsError ? request.ToHttpResult() : (await service.Handle(request.Value, ct).ConfigureAwait(false)).ToHttpResult();
        });
        _ = group.MapPost("/todos/{todoId}/change", static async (string todoId, ChangeTodoBody body, string? fence, IService<ChangeTodoRequest, TodoAccepted> service, CancellationToken ct) =>
        {
            var id = TodoWire.Id(todoId);
            if (id.IsError)
            {
                return id.ToHttpResult();
            }

            var request = body.ToRequest(id.Value, TodoWire.Piped(fence));
            return request.IsError ? request.ToHttpResult() : (await service.Handle(request.Value, ct).ConfigureAwait(false)).ToHttpResult();
        });
        _ = group.MapPost("/todos/note/check", static async (CheckTodoNoteBody? body, IService<CheckTodoNoteRequest, CheckTodoNoteResponse> service, CancellationToken ct) =>
            (await service.Handle((body ?? new CheckTodoNoteBody()).ToRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = Mark(group, "done", TodoVerb.Done);
        _ = Mark(group, "reopen", TodoVerb.Reopen);
        _ = Mark(group, "remove", TodoVerb.Remove);
        _ = Mark(group, "restore", TodoVerb.Restore);
        return group;
    }

    private static RouteHandlerBuilder Mark(RouteGroupBuilder group, string segment, TodoVerb verb) =>
        group.MapPost("/todos/{todoId}/" + segment, async (string todoId, MarkTodoBody? body, string? fence, IService<MarkTodoRequest, TodoAccepted> service, CancellationToken ct) =>
        {
            var id = TodoWire.Id(todoId);
            return id.IsError ? id.ToHttpResult() : (await service.Handle((body ?? new MarkTodoBody()).ToRequest(id.Value, verb, TodoWire.Piped(fence)), ct).ConfigureAwait(false)).ToHttpResult();
        });

    // a session always reads fenced text; anyone else when the CLI says its stdout is no terminal
    internal static bool Fenced(ICallerContextAccessor callers, string? fence) => callers.Current?.Class == CallerClass.Session || TodoWire.Piped(fence);

    private static Result<TodoListState> State(string? state) => state switch
    {
        null or "open" => TodoListState.Open,
        "done" => TodoListState.Done,
        "all" => TodoListState.All,
        _ => SindriErrors.InvalidInput.With($"state is open, done or all, not '{Core.Text.SafeText.Line(Core.Text.SafeText.Escape(state), 40)}' (fix: ?state=open)"),
    };
}
