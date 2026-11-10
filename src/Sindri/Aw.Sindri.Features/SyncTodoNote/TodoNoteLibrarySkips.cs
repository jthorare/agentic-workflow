using Aw.Context.Core.Library;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>Keeps the Obsidian todo note out of the design-doc library: its vault is a library root, and Sindri syncs the note itself (todo spec §4.5), so the feed must not read it as a doc.</summary>
/// <param name="locator">Finds the note the profile names.</param>
internal sealed class TodoNoteLibrarySkips(INoteLocator locator) : ILibrarySkips
{
    public async ValueTask<IReadOnlyList<string>> In(string root, CancellationToken cancellationToken)
    {
        var located = await locator.Locate(cancellationToken).ConfigureAwait(false);   // why: a key that can't be resolved skips nothing; the todo row says why
        return located is { IsSuccess: true, Value: { } place } && string.Equals(place.Vault, root, StringComparison.Ordinal)
            ? [place.Note.Replace('\\', '/')]
            : [];
    }
}
