using Aw.Kernel;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>The note sync (todo spec §4.5). Red stub: the pass lands in the next commit.</summary>
/// <param name="deps">What a pass reaches.</param>
internal sealed class TodoNoteSync(SyncDeps deps) : ITodoNoteSync
{
    public TodoNoteStatus Status => throw new NotSupportedException(deps.ToString());

    public ValueTask<TodoNoteStatus> Pass(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<TodoNoteImport>> Import(TodoNoteImportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
