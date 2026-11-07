namespace Aw.Sindri.Core.Reviews;

/// <summary>Finds the live agent session working on a PR's head branch (PR review page spec §8.2, step 3).</summary>
public interface IFixSessions
{
    /// <summary>A live session whose checkout's origin is the head repository and whose branch is the head branch; sessions Sindri launched are skipped.</summary>
    /// <param name="headRepo">The head repository as "owner/name".</param>
    /// <param name="branch">The head branch.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>"&lt;provider&gt;, &lt;working directory with ~&gt;", or null when none matches.</returns>
    public ValueTask<string?> OnBranch(string headRepo, string branch, CancellationToken cancellationToken);
}
