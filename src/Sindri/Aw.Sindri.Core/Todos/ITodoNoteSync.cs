using Aw.Kernel;

namespace Aw.Sindri.Core.Todos;

/// <summary>
/// The note sync (todo spec §4.5): one pass at a time, run by the watch host on its 60 s check, a file-system trigger, a todo event not 'by: obsidian'
/// or a pick window closing; and by the import route. Implementations serialize passes and imports.
/// </summary>
public interface ITodoNoteSync
{
    /// <summary>The status as of the last pass.</summary>
    public TodoNoteStatus Status { get; }

    /// <summary>Runs one pass: read, merge, append, write (§4.5 Writing steps 1-5). A failure is in <see cref="Status"/>, never thrown.</summary>
    /// <param name="cancellationToken">Stops the pass between steps.</param>
    /// <returns>The status after it.</returns>
    public ValueTask<TodoNoteStatus> Pass(CancellationToken cancellationToken);

    /// <summary>The first import (REQ-E6): re-reads the note, refuses SND-TODO-409 when its importable open count isn't the one the owner saw, SND-TODO-008 when it has none, then adopts and records 'todo.note.imported'.</summary>
    /// <param name="request">The note the caller saw, the count, and who asked.</param>
    /// <param name="cancellationToken">Stops it.</param>
    /// <returns>What it found and did.</returns>
    public ValueTask<Result<TodoNoteImport>> Import(TodoNoteImportRequest request, CancellationToken cancellationToken);
}
