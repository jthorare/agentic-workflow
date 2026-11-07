using Aw.Context.Core.Audience;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>The library's state for the Reviews header. Today's adapter counts docs from the library reads and knows no unreadable roots; the library feed's root state replaces only the adapter.</summary>
internal interface IReviewLibraryState
{
    /// <summary>Reads the counts the header shows.</summary>
    /// <param name="audience">The caller's audiences.</param>
    /// <param name="source">The library source's sync progress, or null when there is none.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The library facts; a library that can't be read counts as no docs.</returns>
    public ValueTask<ReviewLibraryFacts> Read(AudienceScope audience, SourceProgress? source, CancellationToken cancellationToken);
}
