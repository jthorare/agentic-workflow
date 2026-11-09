using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Host.Server;

/// <summary>Runs one note pass as the caller 'service:todo-note' (the events a pass writes name it).</summary>
/// <param name="sync">The note sync.</param>
/// <param name="callers">Names the caller.</param>
internal sealed class NotePasses(ITodoNoteSync sync, ICallerContextAccessor callers)
{
    /// <summary>Runs a pass. A failure is in the sync's status, never thrown.</summary>
    /// <param name="cancellationToken">Stops the pass between steps.</param>
    /// <returns>When the pass ended.</returns>
    public async ValueTask Run(CancellationToken cancellationToken)
    {
        using var caller = callers.EnterService("todo-note");
        _ = await sync.Pass(cancellationToken).ConfigureAwait(false);
    }
}
