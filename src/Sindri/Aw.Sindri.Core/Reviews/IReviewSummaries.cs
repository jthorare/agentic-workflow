using Aw.Kernel;

namespace Aw.Sindri.Core.Reviews;

/// <summary>
/// The per-head summary cache under <c language="text">$AW_SINDRI_HOME/reviews/cache/&lt;owner&gt;/&lt;repo&gt;/&lt;number&gt;/&lt;sha&gt;/summary.json</c> (owner-only; PR review page spec §6.2 "Cache"). A reference or sha that
/// fails GitHub's name rules is refused before it builds a path.
/// </summary>
public interface IReviewSummaries
{
    /// <summary>Reads the summary of one head.</summary>
    /// <param name="pull">The PR.</param>
    /// <param name="sha">The head commit.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The summary, null when none is stored for that head, or the error for a bad name.</returns>
    public ValueTask<Result<ReviewSummary?>> Read(ReviewPullRef pull, string sha, CancellationToken cancellationToken);

    /// <summary>Reads the newest summary of the PR, of whatever head.</summary>
    /// <param name="pull">The PR.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The newest by <see cref="ReviewSummary.At"/>, null when none, or the error for a bad name.</returns>
    public ValueTask<Result<ReviewSummary?>> Newest(ReviewPullRef pull, CancellationToken cancellationToken);

    /// <summary>Writes the summary of its head, replacing one already there.</summary>
    /// <param name="pull">The PR.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the error.</returns>
    public ValueTask<Result<Unit>> Write(ReviewPullRef pull, ReviewSummary summary, CancellationToken cancellationToken);
}
