using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The todo note's setup calls (<see cref="SindriApiClient"/>, continued; todo spec §4.4).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<TodoNoteCheckView>> CheckTodoNote(Uri api, string vault, string note, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<TodoNoteImportedView>> ImportTodoNote(Uri api, string vault, string note, int expectOpen, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<TodoNoteChangedView>> SetTodoNote(Uri api, string vault, string note, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<TodoNoteChangedView>> StopTodoNote(Uri api, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
