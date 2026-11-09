using Aw.Kernel;

namespace Aw.Sindri.Core.Todos;

/// <summary>Reads the todo note. The sync (todo Ph3b's note-service PR) registers the real one; until then <see cref="UnsetTodoNoteSource"/> answers "no note" and a pick checks only what the ledger holds.</summary>
public interface ITodoNoteSource
{
    /// <summary>Reads the note now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The note, null when none is set, or the read error.</returns>
    public ValueTask<Result<TodoNoteSnapshot?>> Read(CancellationToken cancellationToken);
}
