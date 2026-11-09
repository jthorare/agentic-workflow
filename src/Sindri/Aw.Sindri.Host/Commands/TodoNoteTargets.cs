using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Commands;

/// <summary>Reads the live profile's 'todo.obsidian' in process, like 'todo note set' does, so 'todo note import' names the note the profile names.</summary>
internal static class TodoNoteTargets
{
    internal static async Task<Result<TodoNoteTarget>> Read(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        var loaded = await services.GetRequiredService<IProfileSource>().Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        return loaded.Value.Profile.Todo?.Obsidian is { Vault: { Length: > 0 } vault, Note: { Length: > 0 } note }
            ? new TodoNoteTarget(vault, note)
            : SindriErrors.TodoNoteKey.With("todo.obsidian isn't set (fix: sindri todo note set <vault> <note>)");
    }
}
