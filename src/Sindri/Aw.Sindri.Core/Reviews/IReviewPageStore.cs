using Aw.Kernel;

namespace Aw.Sindri.Core.Reviews;

/// <summary>Context's stored side of the PR page (spec §6.2): the PR record, its claims and the doc map, read for the current caller. Reads stored data only.</summary>
public interface IReviewPageStore
{
    /// <summary>Reads what is stored about one PR.</summary>
    /// <param name="pull">The PR.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stored facts (a PR with no record has none of them), or the error.</returns>
    public ValueTask<Result<ReviewPageStored>> Read(ReviewPullRef pull, CancellationToken cancellationToken);
}
