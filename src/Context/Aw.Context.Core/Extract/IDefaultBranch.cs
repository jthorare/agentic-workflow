using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Extract;

/// <summary>Where a checkout's default branch is, read from local refs only (never a fetch).</summary>
public interface IDefaultBranch
{
    /// <summary>Whether a commit is on the default branch: reachable from origin/HEAD, else refs/heads/main, else refs/heads/master.</summary>
    /// <param name="repo">The repo: its name and checkout.</param>
    /// <param name="commit">The commit (a full sha).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the default branch contains it; false when it doesn't or no default ref exists; SND-CONTEXT-048 when the checkout can't be read.</returns>
    public ValueTask<Result<bool>> Reaches(ContextRepo repo, string commit, CancellationToken cancellationToken);
}
