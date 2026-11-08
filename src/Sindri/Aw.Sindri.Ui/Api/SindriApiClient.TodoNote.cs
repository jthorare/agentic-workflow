using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api;

/// <summary>The todo note calls (<see cref="SindriApiClient"/>, continued; todo spec §4.4): the note's state and the conflict and hold picks.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<TodoNoteView>> TodoNote(Uri api, CancellationToken cancellationToken) =>
        Get<TodoNoteView>(api, TodoRoutes.Note, cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<TodoKeepAcceptedView>> KeepTodo(Uri api, string id, string side, CancellationToken cancellationToken) =>
        TodoPick<TodoKeepAcceptedView>(api, TodoRoutes.Keep(id), AwJson.Serialize(new TodoKeepBody(side), Json), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoKeepUndoneView>> UndoKeepTodo(Uri api, string id, CancellationToken cancellationToken) =>
        TodoPick<TodoKeepUndoneView>(api, TodoRoutes.KeepUndo(id), body: null, cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoHoldPickedView>> ApplyHold(Uri api, string noteHash, CancellationToken cancellationToken) =>
        TodoPick<TodoHoldPickedView>(api, TodoRoutes.HoldApply, AwJson.Serialize(new TodoHoldBody(noteHash), Json), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoHoldPickedView>> RestoreHold(Uri api, string noteHash, CancellationToken cancellationToken) =>
        TodoPick<TodoHoldPickedView>(api, TodoRoutes.HoldRestore, AwJson.Serialize(new TodoHoldBody(noteHash), Json), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoHoldUndoneView>> UndoHold(Uri api, CancellationToken cancellationToken) =>
        TodoPick<TodoHoldUndoneView>(api, TodoRoutes.HoldUndo, body: null, cancellationToken);

    private Task<Result<T>> TodoPick<T>(Uri api, string route, byte[]? body, CancellationToken cancellationToken) =>
        Call<T>(api, HttpMethod.Post, route, cancellationToken, new CallOptions(Content: body));
}
