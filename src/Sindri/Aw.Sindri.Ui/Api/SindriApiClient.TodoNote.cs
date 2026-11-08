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
    public Task<Result<TodoPickAcceptedView>> KeepTodo(Uri api, string id, string side, CancellationToken cancellationToken) =>
        TodoPick(api, TodoRoutes.Keep(id), AwJson.Serialize(new TodoKeepBody(side), Json), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoPickAcceptedView>> UndoKeepTodo(Uri api, string id, CancellationToken cancellationToken) =>
        TodoPick(api, TodoRoutes.KeepUndo(id), body: null, cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoPickAcceptedView>> ApplyHold(Uri api, string noteHash, CancellationToken cancellationToken) =>
        TodoPick(api, TodoRoutes.HoldApply, AwJson.Serialize(new TodoHoldBody(noteHash), Json), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoPickAcceptedView>> RestoreHold(Uri api, string noteHash, CancellationToken cancellationToken) =>
        TodoPick(api, TodoRoutes.HoldRestore, AwJson.Serialize(new TodoHoldBody(noteHash), Json), cancellationToken);

    /// <inheritdoc />
    public Task<Result<TodoPickAcceptedView>> UndoHold(Uri api, CancellationToken cancellationToken) =>
        TodoPick(api, TodoRoutes.HoldUndo, body: null, cancellationToken);

    private Task<Result<TodoPickAcceptedView>> TodoPick(Uri api, string route, byte[]? body, CancellationToken cancellationToken) =>
        Call<TodoPickAcceptedView>(api, HttpMethod.Post, route, cancellationToken, new CallOptions(Content: body));
}
