using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Todos.Note;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>Resolves the live profile's 'todo.obsidian' to the vault's folder, the way Check note does (todo spec §4.5 "Setting it").</summary>
/// <param name="live">The live profile.</param>
/// <param name="scopes">Opens a scope per lookup: the locator is a singleton and the context engine is scoped.</param>
/// <param name="host">The machine's folders, to expand a vault's '~/'.</param>
internal sealed class TodoNoteLocator(IProfileSource live, IServiceScopeFactory scopes, LibraryHost host) : INoteLocator
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

        var vault = key.Vault ?? string.Empty;   // why: Problem refused an empty one above
        var root = VaultRoot(profile, vault);
        return root.IsError ? root.Error : new NotePlace(vault, key.Note ?? string.Empty, root.Value);
    }

    private Result<string> VaultRoot(SindriProfile profile, string vault)
    {
        using var scope = scopes.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IContextEngine>().CheckSettings(profile.Context, [.. (profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))]);
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
