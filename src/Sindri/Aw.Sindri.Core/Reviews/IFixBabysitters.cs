namespace Aw.Sindri.Core.Reviews;

/// <summary>Whether a PR babysitter instance is watching a pull request (PR review page spec §8.2, step 3).</summary>
public interface IFixBabysitters
{
    /// <summary>Whether a live instance waits on this pull request.</summary>
    /// <param name="link">The pull request's link, as the forge port names it.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>True when a babysitter watches it; false also when the lookup failed.</returns>
    public ValueTask<bool> Watching(string link, CancellationToken cancellationToken);
}
