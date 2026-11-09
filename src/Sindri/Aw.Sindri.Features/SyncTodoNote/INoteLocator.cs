using Aw.Kernel;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>Finds the note the live profile names (todo spec §4.5): a port, so the sync's tests need no profile, context engine or vault folder.</summary>
internal interface INoteLocator
{
    /// <summary>Reads the live profile's 'todo.obsidian' and resolves its vault.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The place, null when no key is set, or why it can't be resolved.</returns>
    public ValueTask<Result<NotePlace?>> Locate(CancellationToken cancellationToken);
}
