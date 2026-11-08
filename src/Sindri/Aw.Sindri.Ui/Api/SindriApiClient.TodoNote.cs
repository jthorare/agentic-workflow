using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The todo note calls (<see cref="SindriApiClient"/>, continued; todo spec §4.4): the note's state and the conflict and hold picks.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<TodoNoteView>> TodoNote(Uri api, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<TodoPickAcceptedView>> KeepTodo(Uri api, string id, string side, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<TodoPickAcceptedView>> UndoKeepTodo(Uri api, string id, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<TodoPickAcceptedView>> ApplyHold(Uri api, string noteHash, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<TodoPickAcceptedView>> RestoreHold(Uri api, string noteHash, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<TodoPickAcceptedView>> UndoHold(Uri api, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
