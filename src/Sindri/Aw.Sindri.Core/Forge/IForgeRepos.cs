using Aw.Kernel;

namespace Aw.Sindri.Core.Forge;

/// <summary>Which forge repository a workspace belongs to: the profile names no remote, so the workspace's own 'origin' decides.</summary>
public interface IForgeRepos
{
    /// <summary>Reads the repository a workspace's 'origin' remote names.</summary>
    /// <param name="workspace">The workspace (a worktree) path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The host, owner and name; an error when the workspace has no readable 'origin'.</returns>
    public ValueTask<Result<PullRepo>> OfWorkspace(string workspace, CancellationToken cancellationToken);
}
