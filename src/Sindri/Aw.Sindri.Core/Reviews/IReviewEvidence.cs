using Aw.Kernel;

namespace Aw.Sindri.Core.Reviews;

/// <summary>Finds the evidence on disk for a PR's head (spec §6.2 table, "evidence"): ui-evidence runs whose summary.json names the head sha, else the PR number.</summary>
public interface IReviewEvidence
{
    /// <summary>Lists what is found.</summary>
    /// <param name="pull">The PR.</param>
    /// <param name="headSha">The head commit.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The items, newest first, or the error (a folder that can't be read).</returns>
    public ValueTask<Result<IReadOnlyList<ReviewPageEvidenceItem>>> Find(ReviewPullRef pull, string headSha, CancellationToken cancellationToken);
}
