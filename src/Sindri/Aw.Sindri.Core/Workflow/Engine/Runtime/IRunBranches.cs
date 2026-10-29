namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Whether a run's branch kept work: the owner reads "its work so far is kept on branch X" only when there is something on it (sandbox spec §8).</summary>
public interface IRunBranches
{
    /// <summary>Tells whether <paramref name="branch"/> has commits the checkout's current branch does not.</summary>
    /// <param name="repository">The repo's checkout.</param>
    /// <param name="branch">The run branch, for example 'sindri/WFI-…/1/work'.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the branch exists and is ahead; false when it is missing, level or the repo can't be read.</returns>
    public ValueTask<bool> HasCommits(string repository, string branch, CancellationToken cancellationToken);
}
