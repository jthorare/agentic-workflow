using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.RefreshDocs;

/// <summary>
/// Refreshes every repo in scope in turn, cleaning with this call's scrubbers (so a changed pattern set cleans the
/// stored documents again); one repo's failure is its row's code, not the whole refresh's.
/// </summary>
internal sealed class RefreshDocsService(IDocIndex index, IScrubberFactory scrubberFactory) : IService<RefreshDocsRequest, RefreshReport>
{
    public async ValueTask<Result<RefreshReport>> Handle(RefreshDocsRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var scrubbers = settings.Bind(s => scrubberFactory.For(s.ExtraPatterns));
        var repos = scrubbers.Bind(_ => ScopeRules.Resolve(settings.Value, request.Scope.Repos, request.Scope.Caller));
        if (repos.IsError)
        {
            return repos.Error;
        }

        var rows = new List<RepoRefresh>();
        foreach (var repo in repos.Value)
        {
            var refreshed = await index.Refresh(repo, settings.Value.IndexedDocGlobs, scrubbers.Value, cancellationToken).ConfigureAwait(false);
            if (refreshed.IsError)
            {
                return refreshed.Error;   // the store itself failed (SND-CONTEXT-004): no later repo can do better
            }

            rows.Add(refreshed.Value);
        }

        return new RefreshReport(RepoDocs.Id, rows);
    }
}
