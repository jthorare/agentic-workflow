using Aw.Kernel;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Testing;

/// <summary>The PR page's evidence lookup, set by the test.</summary>
public sealed class FakeReviewEvidence : IReviewEvidence
{
    private Result<IReadOnlyList<ReviewPageEvidenceItem>> _items = Results.Success<IReadOnlyList<ReviewPageEvidenceItem>>([]);

    /// <summary>Sets what the next lookups find.</summary>
    /// <param name="items">The items.</param>
    /// <returns>This fake.</returns>
    public FakeReviewEvidence With(params ReviewPageEvidenceItem[] items)
    {
        _items = Results.Success<IReadOnlyList<ReviewPageEvidenceItem>>(items);
        return this;
    }

    public ValueTask<Result<IReadOnlyList<ReviewPageEvidenceItem>>> Find(ReviewPullRef pull, string headSha, CancellationToken cancellationToken) => ValueTask.FromResult(_items);
}
