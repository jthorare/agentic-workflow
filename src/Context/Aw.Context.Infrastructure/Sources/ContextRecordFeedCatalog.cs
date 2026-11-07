using Aw.Context.Core.Engine;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Linear;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Infrastructure.Sources;

/// <summary>The feeds of the sources inside Context, in a stable order: repo docs over the repos in scope, then the design-doc library (the 'aw' root and the vaults; it runs with no repo in scope), then Linear when the profile names a 'context.sources.linear' token, then the external feeds the Host adds (GitHub). No repo in scope gives only the library, as <see cref="ContextSourceCatalog"/> gives none.</summary>
internal sealed class ContextRecordFeedCatalog(IRecordStore store, GitCli git, LinearSources linear, IEnumerable<IExternalRecordFeeds>? external = null, LibraryParts? library = null, ILogger<RepoDocsFeed>? repoDocsLog = null) : IRecordFeedCatalog
{
    public IReadOnlyList<RecordFeed> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(scrubbers);
        var feeds = new List<RecordFeed>(3);
        if (repos.Count > 0)
        {
            feeds.Add(new RepoDocsFeed(repos, settings.IndexedDocGlobs, settings.GitHub?.Repos ?? [], store, git, scrubbers, repoDocsLog));
        }

        if (LibraryFeedFor(settings, scrubbers) is { } libraryFeed)
        {
            feeds.Add(libraryFeed);
        }

        if (repos.Count == 0)
        {
            return feeds;   // Linear and the external feeds need a repo in scope
        }

        if (settings.Linear is { } linearSettings)
        {
            feeds.Add(linear.CreateFeed(linearSettings, settings.GitHub?.Repos));
            feeds.Add(linear.CreateProjectFeed(linearSettings));
        }

        feeds.AddRange((external ?? []).SelectMany(e => e.For(settings, repos, scrubbers)));
        return feeds;
    }

    // The library reads the 'aw' root by default, so a profile that names no 'context.sources.library' still has it; a root the path rules refuse is left out (the settings check says why).
    private LibraryFeed? LibraryFeedFor(ContextSettings settings, Scrubbers scrubbers)
    {
        if (library is null)
        {
            return null;
        }

        var roots = LibraryPaths.Resolve(settings.Library ?? new LibrarySettings(), library.Host);
        return roots.IsError   // why: a profile with no root left (the vault dropped, or stateDir: off) still gets the feed, so it can remove what those roots left behind
            ? null
            : new LibraryFeed(new LibrarySetup(roots.Value, settings.GitHub?.Repos ?? []), store, scrubbers, library.Status, library.Clock);
    }
}
