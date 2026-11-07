using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Library;
using Aw.Context.Core.Sync;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>The library header over <see cref="IContextLibrary"/>: docs and docs with errors from the library reads, the unreadable folders and a first read still running from the feed's root state.</summary>
/// <param name="library">Context's library reads.</param>
/// <param name="host">The machine's home folders, to write a folder the way the profile does.</param>
internal sealed class ContextLibraryState(IContextEngine library, LibraryHost host) : IReviewLibraryState
{
    public async ValueTask<ReviewLibraryFacts> Read(ContextSettings settings, ContextScope scope, AudienceScope audience, SourceProgress? source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var docs = await library.ListDocs(new LibraryDocFilter(), audience, cancellationToken).ConfigureAwait(false);
        var list = docs.IsError ? [] : docs.Value;

        // why: a session's Roots answers SND-CONTEXT-020, and a read that fails says nothing about the folders, so both count as no folders
        var read = await library.Roots(scope, cancellationToken).ConfigureAwait(false);
        var roots = read.IsError ? [] : read.Value;
        var folders = LibraryFolders.Of(settings.Library, host);
        var unreadable = roots.Where(static r => r.State == LibraryRootState.Unreadable).Select(r => new ReviewUnreadableRoot(folders.Root(r.Root), r.Reason ?? "it can't be read")).ToList();
        var reading = source is { Usable: false, LastCode: null } || roots.Any(static r => r.State == LibraryRootState.Reading);
        return new ReviewLibraryFacts(reading, list.Count, list.Count(static d => d.Errors.Count > 0), unreadable);
    }
}
