using Aw.Kernel;
using Aw.Sindri.Ui.Api.Todos;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The todo calls of the fake (todo spec §4.4): each answers the API's goldens, the same text the handler serves, and records what the app sent.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<string> _todosStates = [];
    private readonly List<TodoWriteSent> _todoWrites = [];
    private int _todosCalls;

    /// <summary>GET /sindri/todos's answer for every state: the "list" golden by default.</summary>
    public RouteAnswer TodosAnswer { get; set; } = RouteAnswer.Api("todos", "list");

    /// <summary>The answer of every todo write; null answers an accepted write for the todo, one version on.</summary>
    public RouteAnswer? TodoWriteAnswer { get; set; }

    /// <summary>When set, the list read waits for it: the panel stays loading until the test completes it.</summary>
    public TaskCompletionSource? TodosGate { get; set; }

    /// <summary>Runs after an accepted todo write (route, body), as the projector would move the todo (a test sets the next answer here).</summary>
    public Action<string, object>? OnTodoWritten { get; set; }

    /// <summary>How many times the list was read.</summary>
    public int TodosCalls => Volatile.Read(ref _todosCalls);

    /// <summary>The state filter of every list read, in order.</summary>
    public IReadOnlyList<string> TodosStates { get { lock (_gate) { return [.. _todosStates]; } } }

    /// <summary>Every todo write received, in order.</summary>
    public IReadOnlyList<TodoWriteSent> TodoWrites { get { lock (_gate) { return [.. _todoWrites]; } } }

    public async Task<Result<TodoListView>> Todos(Uri api, string state, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _todosCalls);
        lock (_gate)
        {
            _todosStates.Add(state);
        }

        await WaitFor(TodosGate, cancellationToken).ConfigureAwait(false);
        return Answer<TodoListView>(api, TodoRoutes.List(state), TodosAnswer);
    }

    public Task<Result<TodoAcceptedView>> AddTodo(Uri api, TodoAdd add, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Add, add, "td_fake_added", version: 0, cancellationToken);

    public Task<Result<TodoAcceptedView>> ChangeTodo(Uri api, string id, TodoChange change, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Change(id), change, id, change.Version, cancellationToken);

    public Task<Result<TodoAcceptedView>> DoneTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Done(id), new TodoVersion(version), id, version ?? 0, cancellationToken);

    public Task<Result<TodoAcceptedView>> ReopenTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Reopen(id), new TodoVersion(version), id, version ?? 0, cancellationToken);

    public Task<Result<TodoAcceptedView>> RemoveTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Remove(id), new TodoVersion(version), id, version ?? 0, cancellationToken);

    public Task<Result<TodoAcceptedView>> RestoreTodo(Uri api, string id, long? version, CancellationToken cancellationToken) =>
        TodoWrite(api, TodoRoutes.Restore(id), new TodoVersion(version), id, version ?? 0, cancellationToken);

    private async Task<Result<TodoAcceptedView>> TodoWrite(Uri api, string route, object body, string id, long version, CancellationToken cancellationToken)
    {
        if (NotReceived<TodoAcceptedView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _todoWrites.Add(new TodoWriteSent(route, body));
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        var accepted = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $$"""{"accepted":{"commandId":"cmd_fake","sequence":41},"id":"{{id}}","version":{{version + 1}},"due":null}""");
        var answer = Answer<TodoAcceptedView>(api, route, TodoWriteAnswer ?? new RouteAnswer(System.Net.HttpStatusCode.OK, accepted));
        if (answer.IsSuccess)
        {
            _ = Interlocked.Exchange(ref _pending, answer.Value.Accepted.Sequence);
            OnTodoWritten?.Invoke(route, body);
        }

        return answer;
    }
}
