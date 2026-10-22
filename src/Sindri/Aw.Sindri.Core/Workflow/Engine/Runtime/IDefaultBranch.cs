using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The repo's default branch, for the PR gates (PB spec §10.1, BD4): the branch a pull request must target to be merged.</summary>
public interface IDefaultBranch
{
    /// <summary>The name of the default branch of the repo checked out at <paramref name="checkout"/>.</summary>
    /// <param name="checkout">A checkout or worktree of the repo.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The branch name without any remote prefix ('main', 'release/1'); an error when the checkout can't tell.</returns>
    public ValueTask<Result<string>> Of(string checkout, CancellationToken cancellationToken);
}
