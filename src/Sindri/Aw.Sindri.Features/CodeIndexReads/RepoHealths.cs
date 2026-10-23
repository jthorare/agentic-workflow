using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>Lays the live heads and the build jobs over a list of repo statuses, for the status read and for GET /status's health.</summary>
internal sealed class RepoHealths(IRepoHeads heads, IJobViews jobs, TimeProvider clock)
{
    // Stale, building and waiting come from the live heads and the build jobs; either read failing leaves the file state as it is.
    internal async ValueTask<IReadOnlyList<RepoHealth>> Of(CodeIndexScope scope, IReadOnlyList<RepoStatus> repos, CancellationToken cancellationToken)
    {
        var live = await heads.Heads(scope.Repos, cancellationToken).ConfigureAwait(false);
        var builds = await jobs.List(state: null, JobKind.From("BuildCodeIndex"), cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        return [.. repos.Select(r => RepoHealth.Of(r, live.IsError ? null : live.Value.FirstOrDefault(h => h.Repo == r.Repo), builds.IsError ? [] : [.. builds.Value.Where(j => IndexBuildWords.IsBuildOf(j, r.Repo.Value))], now))];
    }
}
