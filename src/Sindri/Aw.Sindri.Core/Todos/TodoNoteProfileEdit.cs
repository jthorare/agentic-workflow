using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Core.Todos;

/// <summary>
/// The one edit of the live profile's 'todo.obsidian' behind 'sindri todo note set' and 'stop' (run in process, no control plane) and the routes POST /sindri/todos/note/set and /stop (todo spec §4.4, §4.5): it plans the edit with
/// <see cref="IProfileKeyWriter"/> (comments and order kept), has the edited text read and checked before anything is written (a bad vault or note is SND-TODO-002 with the key path), writes it atomically and reads the live hash back.
/// An <c xml:lang="en">ifHash</c> that isn't the live profile's hash is SND-TODO-409 and nothing is written. Live at once: nothing is approved.
/// </summary>
/// <param name="writer">Plans and writes the edit.</param>
/// <param name="parser">Reads the edited text.</param>
/// <param name="live">The live profile, for the guard and the hash.</param>
public sealed class TodoNoteProfileEdit(IProfileKeyWriter writer, IProfileTextParser parser, IProfileSource live)
{
    private const string Key = "todo.obsidian";

    /// <summary>Sets both fields of 'todo.obsidian'.</summary>
    /// <param name="vault">The vault name.</param>
    /// <param name="note">The note path inside it.</param>
    /// <param name="ifHash">The live profile's hash the caller saw, or null to write whatever is live.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The edit; SND-TODO-002, SND-TODO-409, SND-PROFILE-001 or SND-PROFILE-002.</returns>
    public async ValueTask<Result<TodoNoteEdit>> Set(string vault, string note, string? ifHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(note);
        return await Apply(ifHash, () => writer.PlanTexts([($"{Key}.vault", vault), ($"{Key}.note", note)]), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes 'todo.obsidian'.</summary>
    /// <param name="ifHash">The live profile's hash the caller saw, or null to write whatever is live.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The edit (unchanged when the key was absent); SND-TODO-409, SND-PROFILE-001 or SND-PROFILE-002.</returns>
    public async ValueTask<Result<TodoNoteEdit>> Stop(string? ifHash, CancellationToken cancellationToken) =>
        await Apply(ifHash, () => writer.PlanRemove(Key), cancellationToken).ConfigureAwait(false);

    private async ValueTask<Result<TodoNoteEdit>> Apply(string? ifHash, Func<Result<ProfileKeyEdit>> plan, CancellationToken cancellationToken)
    {
        var guard = await Guard(ifHash, cancellationToken).ConfigureAwait(false);
        if (guard is not null)
        {
            return guard;
        }

        var planned = plan();
        if (planned.IsError)
        {
            return planned.Error;
        }

        if (planned.Value.Changed && parser.Parse(planned.Value.Text, planned.Value.Path) is { IsError: true } parsed)
        {
            return parsed.Error;   // SND-TODO-002 with the key path, or the profile's own refusal; nothing was written
        }

        var saved = writer.Save(planned.Value);
        if (saved.IsError)
        {
            return saved.Error;
        }

        var after = await live.Load(cancellationToken).ConfigureAwait(false);
        return after.IsError ? after.Error : new TodoNoteEdit(saved.Value, planned.Value.Changed, after.Value.Hash.Value);
    }

    private async ValueTask<Error?> Guard(string? ifHash, CancellationToken cancellationToken)
    {
        if (ifHash is null)
        {
            return null;
        }

        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        return string.Equals(loaded.Value.Hash.Value, ifHash, StringComparison.OrdinalIgnoreCase)
            ? null
            : SindriErrors.TodoConflict.With("the profile changed since you read it (fix: reload the settings and try again)");
    }
}
