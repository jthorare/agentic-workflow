using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Linear;
using Aw.Context.Infrastructure.Onyx;
using Aw.Context.Infrastructure.Store;

namespace Aw.Context.Infrastructure.Sources;

/// <summary>
/// The sources for one call, in a stable order: repo-docs over the repos in scope, then the onyx source when the profile
/// names 'context.sources.onyx' and the linear source when it names a 'context.sources.linear' token (each built per call, as <see cref="OnyxSources"/> and <see cref="LinearSources"/> ask). No repo in scope gives no source at
/// all, onyx included (SourceCatalogContract): a caller whose scope maps no repo reads nothing. Every caller reads every source in scope (owner q93, 2026-10-23). Repo-docs cleans with the call's scrubbers (the
/// profile's extra patterns included); the onyx source returns raw text, which the query cleans at the ingest boundary
/// with those same scrubbers and scrubs again per field on egress.
/// </summary>
internal sealed class ContextSourceCatalog(ContextDatabase database, GitCli git, TimeProvider clock, OnyxSources onyx, LinearSources linear) : ISourceCatalog
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

        var sources = new List<IContextSource>(3) { new RepoDocsSource(repos, database, git, scrubbers, clock) };

        if (settings.Onyx is { } onyxSettings)
        {
            sources.Add(onyx.Create(onyxSettings, new HashSet<string>(settings.LinearTeams, StringComparer.Ordinal)));
        }

        if (settings.Linear is { } linearSettings)
        {
            sources.Add(linear.Create(linearSettings));
        }

        return sources;
    }
}
