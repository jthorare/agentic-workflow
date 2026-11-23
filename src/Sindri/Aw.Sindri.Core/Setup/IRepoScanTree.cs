namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The file system as the repo scan reads it (first-run spec §3.3): the home folder, child folders without links, a folder's '.git' entry, a main checkout's worktree count and its origin URL.
/// The scan never writes. Kept honest by the RepoScanTreeContract (tests/Aw.Sindri.Testing).
/// </summary>
public interface IRepoScanTree
{
    /// <summary>Gets the user's home folder, as a real path.</summary>
    public string Home { get; }

    /// <summary>The real path of <paramref name="path"/> when it is a folder that can be read, else null.</summary>
    /// <param name="path">An absolute path.</param>
    /// <returns>The real path, or null when it is missing, not a folder or unreadable.</returns>
    public string? Real(string path);

    /// <summary>The child folders of <paramref name="folder"/>, sorted by name; a symbolic link is never one of them. An unreadable folder has none.</summary>
    /// <param name="folder">A folder's real path.</param>
    /// <returns>The children's paths.</returns>
    public IReadOnlyList<string> Folders(string folder);

    /// <summary>What <paramref name="folder"/>'s '.git' entry is.</summary>
    /// <param name="folder">A folder's real path.</param>
    /// <returns>The kind of entry.</returns>
    public GitMarker Marker(string folder);

    /// <summary>How many linked worktrees the main checkout <paramref name="checkout"/> has (the entries of its '.git/worktrees').</summary>
    /// <param name="checkout">A main checkout's real path.</param>
    /// <returns>The count; 0 when it has none.</returns>
    public int Worktrees(string checkout);

    /// <summary>The URL of the checkout's 'origin' remote, as git holds it (user name and token included), or null when it has none.</summary>
    /// <param name="checkout">A main checkout's real path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The URL, or null.</returns>
    public ValueTask<string?> Origin(string checkout, CancellationToken cancellationToken);
}
