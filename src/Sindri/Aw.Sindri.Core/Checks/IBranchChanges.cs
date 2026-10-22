using Aw.Kernel;

namespace Aw.Sindri.Core.Checks;

/// <summary>
/// Reads what a branch changed (line spec §7.2, the BLD-* rules): measured from the repository, never reported by the builder. The build checks read the paths for the scope gate
/// (BLD-SCOPE-001, BLD-TEST-001), the added lines for the secret scan (BLD-SECRET-001) and the commits for BLD-COMMIT-001; the ship GREEN gate scans the same lines again.
/// </summary>
public interface IBranchChanges
{
    /// <summary>The changes in <paramref name="worktree"/> from <paramref name="baseSha"/> to <paramref name="head"/>.</summary>
    /// <param name="worktree">A checkout of the repository.</param>
    /// <param name="baseSha">The commit the branch builds on.</param>
    /// <param name="head">The branch's head.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The changes; an error when a commit is unknown or the repository cannot be read.</returns>
    public ValueTask<Result<BranchChanges>> Between(string worktree, string baseSha, string head, CancellationToken cancellationToken);
}
