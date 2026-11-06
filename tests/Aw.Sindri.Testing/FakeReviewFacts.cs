using Aw.Kernel;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Testing;

/// <summary>The Reviews list's facts, set by the test: what Context's stored PR records and sources would give.</summary>
public sealed class FakeReviewFacts : IReviewFacts
{
    private Result<ReviewFactsSnapshot> _snapshot = new ReviewFactsSnapshot([], Github: null, new ReviewLibraryFacts(Reading: false, Docs: 0, WithErrors: 0, UnreadableRoots: []));

    /// <summary>Gets how many times the facts were read.</summary>
    public int Reads { get; private set; }

    /// <summary>Sets what the next reads answer.</summary>
    /// <param name="snapshot">The facts.</param>
    /// <returns>This fake.</returns>
    public FakeReviewFacts With(ReviewFactsSnapshot snapshot)
    {
        _snapshot = snapshot;
        return this;
    }

    /// <summary>Makes every read fail.</summary>
    /// <param name="error">The error.</param>
    /// <returns>This fake.</returns>
    public FakeReviewFacts Failing(Error error)
    {
        _snapshot = error;
        return this;
    }

    public ValueTask<Result<ReviewFactsSnapshot>> Read(CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult(_snapshot);
    }
}
