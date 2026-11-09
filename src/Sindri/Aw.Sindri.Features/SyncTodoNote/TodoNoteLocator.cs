using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>Resolves the live profile's 'todo.obsidian' to the vault's folder, the way Check note does (todo spec §4.5 "Setting it").</summary>
/// <param name="live">The live profile.</param>
/// <param name="context">Reads the library settings.</param>
/// <param name="host">The machine's folders, to expand a vault's '~/'.</param>
internal sealed class TodoNoteLocator(IProfileSource live, IContextEngine context, LibraryHost host) : INoteLocator
{
    public async ValueTask<Result<NotePlace?>> Locate(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var profile = loaded.Value.Profile;
        if (profile.Todo?.Obsidian is not { } key)
        {
            return Results.Success<NotePlace?>(value: null);
        }

        var problem = TodoObsidianRules.Problem(key.Vault, key.Note, profile.Context);
        if (problem is not null)
        {
            return problem;
        }

        var root = VaultRoot(profile, key.Vault);
        return root.IsError ? root.Error : new NotePlace(key.Vault, key.Note, root.Value);
    }

    private Result<string> VaultRoot(SindriProfile profile, string vault)
    {
        var settings = context.CheckSettings(profile.Context, [.. (profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))]);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var roots = settings.Value.Library is { } library ? LibraryPaths.Resolve(library, host) : null;
        var found = roots is { IsSuccess: true } ? roots.Value.FirstOrDefault(r => string.Equals(r.Name, vault, StringComparison.Ordinal) && !r.IsAw) : null;
        return found is null
            ? SindriErrors.TodoNoteKey.With($"todo.obsidian.vault: '{vault}' can't be resolved to a folder (fix: check its path under context.sources.library.vaults)")
            : found.Path;
    }
}
