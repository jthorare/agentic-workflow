using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// Detached scratch checkouts of a repo at one commit (LN-LCH2): where the holdout evaluator runs the app from, outside the phase worktree.
/// Both calls are idempotent, so a relaunch or a cleanup that runs twice changes nothing more.
/// </summary>
public interface IScratchCheckouts
{
    /// <summary>Checks <paramref name="headSha"/> out at <paramref name="path"/>; a checkout already there at that commit is kept.</summary>
    /// <param name="repoPath">The repo to check out of.</param>
    /// <param name="path">Where the checkout goes: absolute, and not yet a different checkout.</param>
    /// <param name="headSha">The commit: 7 to 64 hex digits.</param>
    /// <param name="cancellationToken">Cancels the checkout.</param>
    /// <returns>The path; SND-REQ-001 for a path that isn't absolute or a sha that isn't hex, SND-LAUNCH-004 when the checkout can't be made.</returns>
    public ValueTask<Result<string>> Create(string repoPath, string path, string headSha, CancellationToken cancellationToken);

    /// <summary>Removes the checkout at <paramref name="path"/>; one that isn't there is fine.</summary>
    /// <param name="path">The checkout's path.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>Success once nothing is left at the path.</returns>
    public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken);
}
