using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Testing;

/// <summary>The PR page's stored side, set by the test: what Context's PR record, claims and doc map would give.</summary>
public sealed class FakeReviewPageStore : IReviewPageStore
{
    private Result<ReviewPageStored> _stored = new ReviewPageStored(Title: null, Author: null, Draft: false, Url: null, UpdatedAt: null, [], new ReviewPageLibraryFacts(Error: null, [], Doc: null, [], [], Hidden: 0));

    private string[]? _synced;

    /// <summary>Gets the PRs the page asked for, in order.</summary>
    public IList<ReviewPullRef> Asked { get; } = [];

    /// <summary>Makes a read of any repo but these SND-REVIEW-004, as the real store does for a repo <c language="text">context.sources.github.repos</c> leaves out.</summary>
    /// <param name="repos">The synced repos, owner/name.</param>
    /// <returns>This fake.</returns>
    public FakeReviewPageStore Syncing(params string[] repos)
    {
        _synced = repos;
        return this;
    }

    /// <summary>Sets what the next reads answer.</summary>
    /// <param name="stored">The stored facts.</param>
    /// <returns>This fake.</returns>
    public FakeReviewPageStore With(ReviewPageStored stored)
    {
        _stored = stored;
        return this;
    }

    /// <summary>Makes every read fail.</summary>
    /// <param name="error">The error.</param>
    /// <returns>This fake.</returns>
    public FakeReviewPageStore Failing(Error error)
    {
        _stored = error;
        return this;
    }

    public ValueTask<Result<ReviewPageStored>> Read(ReviewPullRef pull, CancellationToken cancellationToken)
    {
        Asked.Add(pull);
        var slug = pull.Owner + "/" + pull.Repo;
        return ValueTask.FromResult(_synced is { } synced && !synced.Contains(slug, StringComparer.OrdinalIgnoreCase)
            ? Results.Failure<ReviewPageStored>(SindriErrors.ReviewNotSynced.With($"Sindri doesn't sync {slug}. Add it to `context.sources.github.repos`."))
            : _stored);
    }
}
