using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Testing;

/// <summary>
/// The scan's file system in memory (first-run spec §3.3): folders, links, main checkouts with an origin, and worktrees pointing at their main checkout. A worktree counts in its main checkout's
/// <see cref="Worktrees"/> the way git's '.git/worktrees' entries do. Kept honest by <see cref="RepoScanTreeContract"/>.
/// </summary>
/// <param name="home">The home folder.</param>
public sealed class FakeRepoScanTree(string home = "/home/joi") : IRepoScanTree
{
    private readonly SortedSet<string> _folders = new(StringComparer.Ordinal) { home };
    private readonly HashSet<string> _links = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unreadable = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _checkouts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _worktrees = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string Home { get; } = home;

    /// <summary>Gets how many origin reads were made.</summary>
    public int OriginReads { get; private set; }

    /// <summary>Adds a plain folder and its parents.</summary>
    /// <param name="path">An absolute path.</param>
    /// <returns>This fake.</returns>
    public FakeRepoScanTree Folder(string path)
    {
        for (var at = path; at.Length > 1 && _folders.Add(at); at = Parent(at))
        {
        }

        return this;
    }

    /// <summary>Adds a main checkout ('.git' folder) with its origin URL.</summary>
    /// <param name="path">An absolute path.</param>
    /// <param name="origin">The origin URL, or null for none.</param>
    /// <returns>This fake.</returns>
    public FakeRepoScanTree Checkout(string path, string? origin = null)
    {
        _ = Folder(path);
        _checkouts[path] = origin;
        return this;
    }

    /// <summary>Adds a linked worktree ('.git' file) of <paramref name="main"/>.</summary>
    /// <param name="path">An absolute path.</param>
    /// <param name="main">Its main checkout's path.</param>
    /// <returns>This fake.</returns>
    public FakeRepoScanTree Worktree(string path, string main)
    {
        _ = Folder(path);
        _worktrees[path] = main;
        return this;
    }

    /// <summary>Adds a symbolic link to a folder: it reads as a folder at its path, and is never a child.</summary>
    /// <param name="path">An absolute path.</param>
    /// <returns>This fake.</returns>
    public FakeRepoScanTree Link(string path)
    {
        _ = Folder(path);
        _ = _links.Add(path);
        return this;
    }

    /// <summary>Makes a folder unreadable: <see cref="Real"/> answers null and it has no children.</summary>
    /// <param name="path">An absolute path.</param>
    /// <returns>This fake.</returns>
    public FakeRepoScanTree Unreadable(string path)
    {
        _ = Folder(path);
        _ = _unreadable.Add(path);
        return this;
    }

    /// <inheritdoc/>
    public string? Real(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var trimmed = path.Length > 1 ? path.TrimEnd('/') : path;
        return _folders.Contains(trimmed) && !_unreadable.Contains(trimmed) ? trimmed : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> Folders(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return _unreadable.Contains(folder)
            ? []
            : [.. _folders.Where(f => string.Equals(Parent(f), folder, StringComparison.Ordinal) && !_links.Contains(f))];
    }

    /// <inheritdoc/>
    public GitMarker Marker(string folder) =>
        _checkouts.ContainsKey(folder) ? GitMarker.Folder : _worktrees.ContainsKey(folder) ? GitMarker.File : GitMarker.None;

    /// <inheritdoc/>
    public int Worktrees(string checkout) => _worktrees.Values.Count(main => string.Equals(main, checkout, StringComparison.Ordinal));

    /// <inheritdoc/>
    public ValueTask<string?> Origin(string checkout, CancellationToken cancellationToken)
    {
        OriginReads++;
        return ValueTask.FromResult(_checkouts.GetValueOrDefault(checkout));
    }

    private static string Parent(string path)
    {
        var last = path.LastIndexOf('/');
        return last <= 0 ? "/" : path[..last];
    }
}
