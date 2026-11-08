using System.Net;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The todo note calls of the fake (todo spec §4.4): the note read answers <see cref="TodoNoteAnswer"/>, each pick answers <see cref="TodoPickAnswer"/> and is recorded.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<TodoPickSent> _todoPicks = [];
    private int _todoNoteCalls;

    /// <summary>GET /sindri/todos/note's answer: a syncing note with nothing to sort out by default. A bare 404 is an older Sindri.</summary>
    public RouteAnswer TodoNoteAnswer { get; set; } = new(HttpStatusCode.OK, TodoNoteSample.Quiet);

    /// <summary>The answer of every pick and every pick's Undo; null answers an accepted pick.</summary>
    public RouteAnswer? TodoPickAnswer { get; set; }

    /// <summary>Runs after an accepted pick (route, body or null), as the projector would move the todo or the hold (a test sets the next answers here).</summary>
    public Action<string, object?>? OnTodoPicked { get; set; }

    /// <summary>How many times the note was read.</summary>
    public int TodoNoteCalls => Volatile.Read(ref _todoNoteCalls);

    /// <summary>Every pick and Undo received, in order: the route and the body (null for an Undo).</summary>
    public IReadOnlyList<TodoPickSent> TodoPicks { get { lock (_gate) { return [.. _todoPicks]; } } }

    public Task<Result<TodoNoteView>> TodoNote(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _todoNoteCalls);
        return Task.FromResult(Answer<TodoNoteView>(api, TodoRoutes.Note, TodoNoteAnswer));
    }

    public Task<Result<TodoKeepAcceptedView>> KeepTodo(Uri api, string id, string side, CancellationToken cancellationToken) =>
        TodoPick<TodoKeepAcceptedView>(api, TodoRoutes.Keep(id), new TodoKeepBody(side), """{"undoUntil":"2026-01-01T00:00:10.0000000Z"}""", cancellationToken);

    public Task<Result<TodoKeepUndoneView>> UndoKeepTodo(Uri api, string id, CancellationToken cancellationToken) =>
        TodoPick<TodoKeepUndoneView>(api, TodoRoutes.KeepUndo(id), body: null, "{}", cancellationToken);

    public Task<Result<TodoHoldPickedView>> ApplyHold(Uri api, string noteHash, CancellationToken cancellationToken) =>
        TodoPick<TodoHoldPickedView>(api, TodoRoutes.HoldApply, new TodoHoldBody(noteHash), """{"kept":"applied","undoUntil":"2026-01-01T00:00:10.0000000Z"}""", cancellationToken);

    public Task<Result<TodoHoldPickedView>> RestoreHold(Uri api, string noteHash, CancellationToken cancellationToken) =>
        TodoPick<TodoHoldPickedView>(api, TodoRoutes.HoldRestore, new TodoHoldBody(noteHash), """{"kept":"restored","undoUntil":"2026-01-01T00:00:10.0000000Z"}""", cancellationToken);

    public Task<Result<TodoHoldUndoneView>> UndoHold(Uri api, CancellationToken cancellationToken) =>
        TodoPick<TodoHoldUndoneView>(api, TodoRoutes.HoldUndo, body: null, """{"kept":null}""", cancellationToken);

    private async Task<Result<T>> TodoPick<T>(Uri api, string route, object? body, string accepted, CancellationToken cancellationToken)
    {
        if (NotReceived<T>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _todoPicks.Add(new TodoPickSent(route, body));
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        var answer = Answer<T>(api, route, TodoPickAnswer ?? new RouteAnswer(HttpStatusCode.OK, accepted));
        if (answer.IsSuccess)
        {
            OnTodoPicked?.Invoke(route, body);
        }

        return answer;
    }
}
