using Aw.Kernel;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos;

/// <summary>A note sync whose status a test sets.</summary>
internal sealed class TodoNoteSyncStub : ITodoNoteSync
{
    public TodoNoteStatus Status { get; set; } = TodoNoteStatus.NotSet;

    public ValueTask<TodoNoteStatus> Pass(CancellationToken cancellationToken) => ValueTask.FromResult(Status);

    public ValueTask<Result<TodoNoteImport>> Import(TodoNoteImportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
