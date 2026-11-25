using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.ScanRepos;

/// <summary>
/// The walk under the scan's root (first-run spec §3.3), breadth first so the shallow repos come first: a folder with a '.git' folder is a main checkout and is listed, one with a '.git' file is a worktree and is
/// neither listed nor entered (its main checkout counts it), and the walk never enters a checkout, a folder <see cref="RepoScanRules.Skips"/> names, a link or a folder past the depth. It stops at the cap or when cancelled.
/// </summary>
/// <param name="tree">The file system.</param>
internal sealed class RepoScanWalk(IRepoScanTree tree)
{
    /// <summary>Gets how many folders the walk looked at.</summary>
    public int Folders { get; private set; }

    /// <summary>Gets how many checkouts it listed.</summary>
    public int Repos { get; private set; }

    /// <summary>The main checkouts under <paramref name="root"/>, as the walk finds them.</summary>
    /// <param name="root">The root's real path.</param>
    /// <param name="depth">How many folders deep the walk goes below the root.</param>
    /// <param name="cancellationToken">Stops the walk.</param>
    /// <returns>The checkouts' real paths.</returns>
    public IEnumerable<string> Checkouts(string root, int depth, CancellationToken cancellationToken)
    {
        var queue = new Queue<(string Folder, int Level)>([(root, 0)]);
        while (queue.Count > 0 && Repos < RepoScanRules.Cap && !cancellationToken.IsCancellationRequested)
        {
            var (folder, level) = queue.Dequeue();
            Folders++;
            var marker = tree.Marker(folder);
            if (marker == GitMarker.Folder)
            {
                Repos++;
                yield return folder;
                continue;
            }

            if (marker == GitMarker.File || level == depth)
            {
                continue;
            }

            foreach (var child in tree.Folders(folder).Where(static child => !RepoScanRules.Skips(Path.GetFileName(child))))
            {
                queue.Enqueue((child, level + 1));
            }
        }
    }
}
