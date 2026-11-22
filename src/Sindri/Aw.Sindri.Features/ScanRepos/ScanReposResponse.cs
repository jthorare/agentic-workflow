namespace Aw.Sindri.Features.ScanRepos;

/// <summary>
/// A scan that starts when <see cref="Rows"/> is read: the rows arrive as the walk finds them, and <see cref="End"/> holds the closing line once they are all read. Stopping the read (the client went away) stops the walk.
/// </summary>
/// <param name="rows">The walk.</param>
/// <param name="end">The closing line, read after the walk.</param>
internal sealed class ScanReposResponse(IAsyncEnumerable<RepoScanRow> rows, Func<RepoScanEnd> end)
{
    /// <summary>Gets the rows, one per main checkout, in the order the walk finds them.</summary>
    public IAsyncEnumerable<RepoScanRow> Rows { get; } = rows;

    /// <summary>The closing line; read it after <see cref="Rows"/> ends.</summary>
    /// <returns>The counts.</returns>
    public RepoScanEnd End() => end();
}
