using Aw.Sindri.Features.SyncTodoNote;

namespace Aw.Sindri.Host.Server;

/// <summary>The folder watch of the todo note (todo spec §4.5 "Reading"): follows the note the live profile names, one non-recursive watch on its folder, and a poke when the file changes.</summary>
/// <param name="locator">Finds the note.</param>
/// <param name="folders">Starts a watch.</param>
internal sealed class NoteFolder(INoteLocator locator, IFolderWatch folders) : IDisposable
{
    private IDisposable? _watch;
    private string? _watched;

    /// <summary>Watches the note's folder (or drops the watch when no key is set or the note can't be placed).</summary>
    /// <param name="changed">Called when the note's file may have changed.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>When the watch matches the profile.</returns>
    public async ValueTask Follow(Action changed, CancellationToken cancellationToken)
    {
        var found = await locator.Locate(cancellationToken).ConfigureAwait(false);
        if (found.IsError || found.Value is not { } place)
        {
            Drop();
            return;
        }

        if (string.Equals(_watched, place.Path, StringComparison.Ordinal))
        {
            return;
        }

        Drop();
        _watch = folders.Watch(Path.GetDirectoryName(place.Path) ?? place.Root, Path.GetFileName(place.Path), changed);
        _watched = place.Path;
    }

    /// <summary>Stops the watch.</summary>
    public void Drop()
    {
        _watch?.Dispose();
        _watch = null;
        _watched = null;
    }

    public void Dispose() => Drop();
}
