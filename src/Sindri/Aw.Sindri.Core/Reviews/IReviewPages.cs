using Aw.Kernel;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The page snapshot per head under <c language="text">$AW_SINDRI_HOME/reviews/cache/&lt;owner&gt;/&lt;repo&gt;/&lt;number&gt;/&lt;sha&gt;/page.json</c> (owner-only; spec §6.2 "Cache"). The newest 3 heads of a PR are kept.</summary>
public interface IReviewPages
{
    /// <summary>Reads the newest cached page of the PR, of whatever head.</summary>
    /// <param name="pull">The PR.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page and when it was cached, null when none is cached, or the error for a bad name.</returns>
    public ValueTask<Result<CachedReviewPage?>> Newest(ReviewPullRef pull, CancellationToken cancellationToken);

    /// <summary>Writes the page under its head, replacing one already there, then drops the heads past the newest 3.</summary>
    /// <param name="pull">The PR.</param>
    /// <param name="page">The page; its head names the folder.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the error.</returns>
    public ValueTask<Result<Unit>> Write(ReviewPullRef pull, ReviewPageModel page, CancellationToken cancellationToken);
}
