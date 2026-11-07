using Aw.Kernel;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The repos the review pages serve (PR review page spec §6.1): those of <c language="text">context.sources.github.repos</c> in the approved profile.</summary>
public interface IReviewRepos
{
    /// <summary>The synced repos as "owner/name".</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The repos, or the error (no approved profile, bad settings, no caller).</returns>
    public ValueTask<Result<IReadOnlyList<string>>> Synced(CancellationToken cancellationToken);
}
