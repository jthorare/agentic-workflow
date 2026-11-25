using Aw.Kernel;

namespace Aw.Sindri.Core.Worktrees;

/// <summary>
/// Port: the worktree of a one-off session (agents spec §15.4). The path comes from skills/_shared/worktree-path.sh, the one worktree root per repo, and the base is the repo's HEAD.
/// It never forces: a tree with changes stays.
/// </summary>
public interface IOneOffWorktrees
{
    /// <summary>Makes a worktree and branch named <paramref name="name"/> off the repo's HEAD; a name that exists gets '-2', '-3'.</summary>
    /// <param name="repoPath">The repo's main checkout.</param>
    /// <param name="name">'oneoff-yyMMdd-HHmm-slug'.</param>
    /// <param name="cancellationToken">Cancels the git calls.</param>
    /// <returns>The worktree, or an error whose message is git's line.</returns>
    public ValueTask<Result<OneOffWorktree>> Make(string repoPath, string name, CancellationToken cancellationToken);

    /// <summary>Runs 'git worktree remove' without --force. A path that is already gone is a success.</summary>
    /// <param name="path">The worktree folder.</param>
    /// <param name="cancellationToken">Cancels the git calls.</param>
    /// <returns>Success, or an error whose message is git's line.</returns>
    public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken);

    /// <summary>The short 'git status' of the worktree, for the SND-AGENT-018 message.</summary>
    /// <param name="path">The worktree folder.</param>
    /// <param name="cancellationToken">Cancels the git call.</param>
    /// <returns>At most 10 status lines; empty for a clean or missing tree.</returns>
    public ValueTask<string> StatusSummary(string path, CancellationToken cancellationToken);
}
