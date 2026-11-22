using System.Runtime.CompilerServices;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.ScanRepos;

/// <summary>
/// One scan's rows (first-run spec §3.3, gap 7): the GitHub read of the account's recent repos runs first, so 'known' is right on the first row; a failed read ticks nothing and the last line says so. Each
/// checkout the walk finds is one row, with its origin cut to host and slug (the user name and token dropped), whether the profile names it, and its worktree count.
/// </summary>
/// <param name="tree">The file system.</param>
/// <param name="known">The account's recent repos.</param>
/// <param name="root">The root's real path.</param>
/// <param name="depth">How deep the walk goes.</param>
/// <param name="inProfile">The real paths of the profile's repos.</param>
internal sealed class RepoScan(IRepoScanTree tree, IKnownRepos known, string root, int depth, IReadOnlySet<string> inProfile)
{
    private const string GitHub = "github.com";

    private readonly RepoScanWalk _walk = new(tree);
    private bool _knownFailed;

    /// <summary>The closing line, once <see cref="Rows"/> has ended.</summary>
    /// <returns>The counts.</returns>
    public RepoScanEnd End() => new(Done: true, _walk.Folders, _walk.Repos, _knownFailed);

    /// <summary>The rows, as the walk finds them.</summary>
    /// <param name="cancellationToken">Stops the read and the walk.</param>
    /// <returns>The rows.</returns>
    public async IAsyncEnumerable<RepoScanRow> Rows([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var read = await known.Slugs(cancellationToken).ConfigureAwait(false);
        _knownFailed = read.IsError;
        var slugs = read.IsSuccess ? read.Value : new HashSet<string>(StringComparer.Ordinal);
        foreach (var checkout in _walk.Checkouts(root, depth, cancellationToken))
        {
            var origin = RepoScanRules.Origin(await tree.Origin(checkout, cancellationToken).ConfigureAwait(false));
            var isKnown = origin is { } o && string.Equals(o.Host, GitHub, StringComparison.Ordinal) && slugs.Contains(o.Slug.ToLowerInvariant());
            yield return new RepoScanRow(checkout, origin?.Host, origin?.Slug, isKnown, inProfile.Contains(checkout), tree.Worktrees(checkout));
        }
    }
}
