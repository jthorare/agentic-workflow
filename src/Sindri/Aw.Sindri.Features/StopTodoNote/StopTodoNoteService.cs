using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.TodoNoteProfile;

namespace Aw.Sindri.Features.StopTodoNote;

/// <summary>Removes 'todo.obsidian' through <see cref="TodoNoteProfileEdit"/>; a key that is already absent is "unchanged".</summary>
/// <param name="edit">The profile edit.</param>
internal sealed class StopTodoNoteService(TodoNoteProfileEdit edit) : IService<StopTodoNoteRequest, TodoNoteWritten>
{
    public async ValueTask<Result<TodoNoteWritten>> Handle(StopTodoNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var done = await edit.Stop(request.IfHash, cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : new TodoNoteWritten(done.Value.Changed, done.Value.ProfileHash);
    }
}
