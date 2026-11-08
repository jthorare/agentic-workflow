using System.Net;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The todo note setup calls of the fake (todo spec §4.4): each answers its <see cref="RouteAnswer"/> and is recorded in order; a set records a <see cref="TodoNoteSetBody"/> and a stop a <see cref="TodoNoteStopBody"/>, both with the ifHash sent.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<TodoNoteSetupSent> _todoNoteSetup = [];

    /// <summary>POST /sindri/todos/note/check's answer: a note that does not exist yet by default.</summary>
    public RouteAnswer TodoNoteCheckAnswer { get; set; } = new(HttpStatusCode.OK, TodoNoteSetupSample.NewNote);

    /// <summary>POST /sindri/todos/note/import's answer.</summary>
    public RouteAnswer TodoNoteImportAnswer { get; set; } = new(HttpStatusCode.OK, TodoNoteSetupSample.Imported);

    /// <summary>POST /sindri/todos/note/set's answer.</summary>
    public RouteAnswer TodoNoteSetAnswer { get; set; } = new(HttpStatusCode.OK, TodoNoteSetupSample.Changed);

    /// <summary>POST /sindri/todos/note/stop's answer.</summary>
    public RouteAnswer TodoNoteStopAnswer { get; set; } = new(HttpStatusCode.OK, TodoNoteSetupSample.Changed);

    /// <summary>When set, each check waits for it before it answers.</summary>
    public TaskCompletionSource? TodoNoteCheckGate { get; set; }

    /// <summary>Runs after an accepted set, import or stop with its route (a test moves the note's next answer here, as the control plane would).</summary>
    public Action<string>? OnTodoNoteSetup { get; set; }

    /// <summary>Every check, import, set and stop received, in order.</summary>
    public IReadOnlyList<TodoNoteSetupSent> TodoNoteSetup { get { lock (_gate) { return [.. _todoNoteSetup]; } } }

    public async Task<Result<TodoNoteCheckView>> CheckTodoNote(Uri api, string vault, string note, CancellationToken cancellationToken)
    {
        if (NotReceived<TodoNoteCheckView>(api) is { } unanswered)
        {
            return unanswered;
        }

        Record(TodoRoutes.NoteCheck, new TodoNoteTarget(vault, note));
        await WaitFor(TodoNoteCheckGate, cancellationToken).ConfigureAwait(false);
        return Answer<TodoNoteCheckView>(api, TodoRoutes.NoteCheck, TodoNoteCheckAnswer);
    }

    public Task<Result<TodoNoteImportedView>> ImportTodoNote(Uri api, string vault, string note, int expectOpen, CancellationToken cancellationToken) =>
        SetupCall<TodoNoteImportedView>(api, TodoRoutes.NoteImport, new TodoNoteImportBody(vault, note, expectOpen), TodoNoteImportAnswer, cancellationToken);

    public Task<Result<TodoNoteChangedView>> SetTodoNote(Uri api, string vault, string note, string? ifHash, CancellationToken cancellationToken) =>
        SetupCall<TodoNoteChangedView>(api, TodoRoutes.NoteSet, new TodoNoteSetBody(vault, note, ifHash), TodoNoteSetAnswer, cancellationToken);

    public Task<Result<TodoNoteChangedView>> StopTodoNote(Uri api, string? ifHash, CancellationToken cancellationToken) =>
        SetupCall<TodoNoteChangedView>(api, TodoRoutes.NoteStop, new TodoNoteStopBody(ifHash), TodoNoteStopAnswer, cancellationToken);

    private async Task<Result<T>> SetupCall<T>(Uri api, string route, object? body, RouteAnswer answer, CancellationToken cancellationToken)
    {
        if (NotReceived<T>(api) is { } unanswered)
        {
            return unanswered;
        }

        Record(route, body);
        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        var result = Answer<T>(api, route, answer);
        if (result.IsSuccess)
        {
            OnTodoNoteSetup?.Invoke(route);
        }

        return result;
    }

    private void Record(string route, object? body)
    {
        lock (_gate)
        {
            _todoNoteSetup.Add(new TodoNoteSetupSent(route, body));
        }
    }
}
