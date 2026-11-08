using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.TodoNoteProfile;

namespace Aw.Sindri.Features.SetTodoNote;

/// <summary>Sets 'todo.obsidian' through <see cref="TodoNoteProfileEdit"/> and answers whether the file changed and the live profile's new hash.</summary>
/// <param name="edit">The profile edit.</param>
internal sealed class SetTodoNoteService(TodoNoteProfileEdit edit) : IService<SetTodoNoteRequest, TodoNoteWritten>
{
    public async ValueTask<Result<TodoNoteWritten>> Handle(SetTodoNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var done = await edit.Set(request.Vault, request.Note, request.IfHash, cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : new TodoNoteWritten(done.Value.Changed, done.Value.ProfileHash);
    }
}
