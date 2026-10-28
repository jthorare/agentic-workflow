using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Linear;

namespace Aw.Context.Infrastructure.Sources;

/// <summary>The feeds of the sources inside Context, in a stable order: repo docs over the repos in scope, then Linear when the profile names a 'context.sources.linear' token. No repo in scope gives none, as <see cref="ContextSourceCatalog"/> does.</summary>
internal sealed class ContextRecordFeedCatalog(IRecordStore store, GitCli git, LinearSources linear) : IRecordFeedCatalog
{
    public IReadOnlyList<RecordFeed> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(scrubbers);
        if (repos.Count == 0)
        {
            return [];
        }

        var feeds = new List<RecordFeed>(2) { new RepoDocsFeed(repos, settings.IndexedDocGlobs, store, git, scrubbers) };
        if (settings.Linear is { } linearSettings)
        {
            feeds.Add(linear.CreateFeed(linearSettings));
        }

        return feeds;
    }
}
