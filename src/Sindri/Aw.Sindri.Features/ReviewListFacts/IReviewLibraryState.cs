using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>The library's state for the Reviews header. The adapter counts docs from the library reads and takes the unreadable roots from the library feed's root state.</summary>
internal interface IReviewLibraryState
{
    /// <summary>Reads the counts the header shows.</summary>
    /// <param name="settings">The checked settings, which name the library's folders.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="audience">The caller's audiences.</param>
    /// <param name="source">The library source's sync progress, or null when there is none.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The library facts; a library that can't be read counts as no docs.</returns>
    public ValueTask<ReviewLibraryFacts> Read(ContextSettings settings, ContextScope scope, AudienceScope audience, SourceProgress? source, CancellationToken cancellationToken);
}
