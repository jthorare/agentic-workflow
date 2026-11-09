using Aw.Kernel;

namespace Aw.Sindri.Core.Todos;

/// <summary>The default source: no note is set, so there is nothing to re-read.</summary>
public sealed class UnsetTodoNoteSource : ITodoNoteSource
{
    /// <inheritdoc/>
    public ValueTask<Result<TodoNoteSnapshot?>> Read(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<TodoNoteSnapshot?>(value: null));
}
