using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.CheckTodoNote;

/// <summary>
/// Looks at a note before it is set (todo spec §4.4, §4.5): the vault and the note path under the key's rules (SND-TODO-002), the note's file (not a symlink, one hard link, readable), the folder (writable, and whether it swaps), then the counts
/// of what the note holds beside the open todos Sindri has. Writes nothing to the vault, the profile or the ledger (the probe's two temporary files are removed). Request class.
/// </summary>
/// <param name="live">The live profile.</param>
/// <param name="context">Reads the library settings.</param>
/// <param name="host">The machine's folders, to expand a vault's '~/'.</param>
/// <param name="views">The todos, for the open count.</param>
/// <param name="disk">The disk side.</param>
/// <param name="callers">The caller; a check needs one.</param>
internal sealed class CheckTodoNoteService(IProfileSource live, IContextEngine context, LibraryHost host, IViewReader views, ITodoNoteDisk disk, ICallerContextAccessor callers)
    : IService<CheckTodoNoteRequest, CheckTodoNoteResponse>
{
    public async ValueTask<Result<CheckTodoNoteResponse>> Handle(CheckTodoNoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("checking the todo note needs a caller");
        }

        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var profile = loaded.Value.Profile;
        if (TodoObsidianRules.Problem(request.Vault, request.Note, profile.Context) is { } bad)
        {
            return bad;
        }

        var root = VaultRoot(profile, request.Vault);
        if (root.IsError)
        {
            return root.Error;
        }

        var looked = Look(root.Value, request);
        if (looked.IsError)
        {
            return looked.Error;
        }

        var open = await views.List<TodoRow>(ViewNames.Todo, cancellationToken).ConfigureAwait(false);
        return open.IsError ? open.Error : Answer(looked.Value, open.Value.Count(TodoDecisions.IsOpen));
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

    private Result<(NoteFileFacts File, NoteFolderFacts Folder)> Look(string root, CheckTodoNoteRequest request)
    {
        if (!Directory.Exists(root))
        {
            return Refused("todo.obsidian.vault", $"the folder of vault '{request.Vault}' isn't there");
        }

        var file = disk.Inspect(root, request.Note);
        if (file.IsSymlink)
        {
            return Refused("todo.obsidian.note", "is, or sits inside, a symlink: Sindri never follows one");
        }

        if (file.Exists && file.LinkCount > 1)
        {
            return Refused("todo.obsidian.note", "has more than one hard link: a swap would split it from the other names");
        }

        if (file.Unreadable is { } why)
        {
            return Refused("todo.obsidian.note", "can't be read: " + why);
        }

        var folder = disk.Probe(root, request.Note);
        return folder.Writable ? (file, folder) : Refused("todo.obsidian.note", "can't be written: Sindri can't create a file in its folder");
    }

    private static Error Refused(string keyPath, string why) => SindriErrors.TodoNoteKey.With($"{keyPath}: {why}");

    private static CheckTodoNoteResponse Answer((NoteFileFacts File, NoteFolderFacts Folder) looked, int sindriOpen)
    {
        var counts = NoteCounter.Count(NoteParser.Parse(looked.File.Text ?? string.Empty));
        return new CheckTodoNoteResponse(looked.File.Exists, counts.Open, counts.Done, counts.Other, counts.CantImport, NoteCounter.WillImport(counts.Importable, sindriOpen), NoteCounter.MaxOpen, sindriOpen, looked.Folder.CanSwap);
    }
}
