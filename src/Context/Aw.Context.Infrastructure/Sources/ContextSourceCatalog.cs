using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Linear;
using Aw.Context.Infrastructure.Store;

namespace Aw.Context.Infrastructure.Sources;

/// <summary>
/// The live sources for one call, in a stable order: repo-docs over the repos in scope, then the linear source when the profile names a 'context.sources.linear' token (built per call, as <see cref="LinearSources"/> asks). The record store's
/// own legs (RecordStoreSource, DenseRecordSource) are added by the query, not here. No repo in scope gives no source
/// at all (SourceCatalogContract): a caller whose scope maps no repo reads nothing. Every caller reads every source in scope (owner q93, 2026-10-23). Repo-docs cleans with the call's scrubbers (the profile's extra patterns included).
/// </summary>
internal sealed class ContextSourceCatalog(ContextDatabase database, GitCli git, TimeProvider clock, LinearSources linear) : ISourceCatalog
{
    public IReadOnlyList<IContextSource> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(scrubbers);
        if (repos.Count == 0)
        {
            return [];
        }

        var sources = new List<IContextSource>(2) { new RepoDocsSource(repos, database, git, scrubbers, clock) };

        if (settings.Linear is { } linearSettings)
        {
            sources.Add(linear.Create(linearSettings));
        }

        return sources;
    }
}
