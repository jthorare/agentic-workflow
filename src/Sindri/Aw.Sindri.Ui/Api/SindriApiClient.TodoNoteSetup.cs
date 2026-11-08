using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api;

/// <summary>The todo note's setup calls (<see cref="SindriApiClient"/>, continued; todo spec §4.4).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<TodoNoteCheckView>> CheckTodoNote(Uri api, string vault, string note, CancellationToken cancellationToken) =>
        Call<TodoNoteCheckView>(api, HttpMethod.Post, TodoRoutes.NoteCheck, cancellationToken, new CallOptions(Content: AwJson.Serialize(new TodoNoteTarget(vault, note), Json)));

    /// <inheritdoc />
    public Task<Result<TodoNoteImportedView>> ImportTodoNote(Uri api, string vault, string note, int expectOpen, CancellationToken cancellationToken) =>
        Call<TodoNoteImportedView>(api, HttpMethod.Post, TodoRoutes.NoteImport, cancellationToken, new CallOptions(Content: AwJson.Serialize(new TodoNoteImportBody(vault, note, expectOpen), Json)));

    /// <inheritdoc />
    public Task<Result<TodoNoteChangedView>> SetTodoNote(Uri api, string vault, string note, CancellationToken cancellationToken) =>
        Call<TodoNoteChangedView>(api, HttpMethod.Post, TodoRoutes.NoteSet, cancellationToken, new CallOptions(Content: AwJson.Serialize(new TodoNoteTarget(vault, note), Json)));

    /// <inheritdoc />
    public Task<Result<TodoNoteChangedView>> StopTodoNote(Uri api, CancellationToken cancellationToken) =>
        Call<TodoNoteChangedView>(api, HttpMethod.Post, TodoRoutes.NoteStop, cancellationToken, new CallOptions(Content: "{}"u8.ToArray()));
}
