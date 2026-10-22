using Aw.Kernel;

namespace Aw.Sindri.Core.Worktrees;

/// <summary>The files a checkout tracks, for the docs-sync station's plain text search (docs-sync spec §7.2).</summary>
public interface ITrackedFiles
{
    /// <summary>The paths git tracks in a checkout or worktree.</summary>
    /// <param name="checkout">The checkout's path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The paths relative to the checkout, '/' separated, in ordinal order; an error when git can't read the checkout.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> Paths(string checkout, CancellationToken cancellationToken);
}
