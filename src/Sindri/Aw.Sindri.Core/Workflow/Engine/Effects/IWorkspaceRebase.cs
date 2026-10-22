using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// The workspace rebase of 'effect.pr.restack' (PB spec §4.6): moves the pull request's commits in the phase's worktree onto the new base,
/// dropping the commits the old base held. A conflict is aborted, so the worktree is as it was. It never pushes; the effect does that, with a lease.
/// </summary>
public interface IWorkspaceRebase
{
    /// <summary>Fetches <paramref name="onto"/> from the workspace's remote and rebases the workspace's branch onto it.</summary>
    /// <param name="workspace">The worktree, on the pull request's head branch.</param>
    /// <param name="onto">The new base branch, as the remote names it.</param>
    /// <param name="oldBase">The commit the old base was at: only the commits after it move (a stacked pull request), or null to move every commit the new base lacks.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How it ended, or the error when the worktree or the remote can't be used.</returns>
    public ValueTask<Result<RebaseOutcome>> Rebase(string workspace, string onto, string? oldBase, CancellationToken cancellationToken);
}
