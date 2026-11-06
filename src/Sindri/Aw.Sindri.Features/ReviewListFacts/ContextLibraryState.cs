using Aw.Context.Core.Audience;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>The library header over <see cref="IContextLibrary"/>: docs and docs with errors; unreadable roots arrive with the library feed.</summary>
/// <param name="library">Context's library reads.</param>
internal sealed class ContextLibraryState(IContextLibrary library) : IReviewLibraryState
{
    public async ValueTask<ReviewLibraryFacts> Read(AudienceScope audience, SourceProgress? source, CancellationToken cancellationToken)
    {
        var docs = await library.ListDocs(new LibraryDocFilter(), audience, cancellationToken).ConfigureAwait(false);
        var list = docs.IsError ? [] : docs.Value;
        return new ReviewLibraryFacts(Reading: source is { Usable: false, LastCode: null }, list.Count, list.Count(static d => d.Errors.Count > 0), UnreadableRoots: []);
    }
}
