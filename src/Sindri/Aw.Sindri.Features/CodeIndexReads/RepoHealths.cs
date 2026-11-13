using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>Lays the live heads and the build jobs over a list of repo statuses, for the status read and for GET /status's health.</summary>
internal sealed class RepoHealths(IRepoHeads heads, IJobViews jobs, IServicePauses pauses, TimeProvider clock, QueuedClock queued)
{
    // Stale, building and waiting come from the live heads and the build jobs; either read failing leaves the file state as it is.
    internal async ValueTask<IReadOnlyList<RepoHealth>> Of(CodeIndexScope scope, IReadOnlyList<RepoStatus> repos, CancellationToken cancellationToken)
    {
        var live = await heads.Heads(scope.Repos, cancellationToken).ConfigureAwait(false);
        var running = await jobs.Running(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<JobRow> runningOf(string repo) => running.IsError ? [] : [.. running.Value.Where(j => IndexBuildWords.IsBuildOf(j, repo))];
        CodeIndex.Core.Model.RepoHead? headOf(RepoStatus r) => live.IsError ? null : live.Value.FirstOrDefault(h => h.Repo == r.Repo);

        // The ended builds are read once, and only when some repo is behind with no build running (issue #2786).
        IReadOnlyList<JobRow> ended = [];
        if (repos.Any(r => RepoHealth.NeedsEnded(r, headOf(r), runningOf(r.Repo.Value))))
        {
            var all = await jobs.List(state: null, JobKind.From("BuildCodeIndex"), cancellationToken).ConfigureAwait(false);
            ended = all.IsError ? [] : all.Value;
        }

        // why: queued promises a build on the next tick; with code-index-main-move paused none comes (#3399). Unqueued before Timed, so a paused repo's queued count stops.
        var active = await MainMovePause.Active(pauses, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        return [.. repos.Select(r => Looked(r, Timed(RepoHealth.Of(r, headOf(r), runningOf(r.Repo.Value), [.. ended.Where(j => IndexBuildWords.IsBuildOf(j, r.Repo.Value))], now).Unqueued(active), now, running.IsError)))];
    }

    // why: a first build that never starts reads queued forever; how long it has is what makes it a problem (#3440). Timed before Looked: the rule reads QueuedFor.
    // why: a failed running-jobs read can't tell queued from building, so it neither starts nor forgets the count (#3481)
    private RepoHealth Timed(RepoHealth health, DateTimeOffset now, bool blind) => blind ? health with { QueuedFor = null } : health with { QueuedFor = queued.For(health.Repo, health.State == "queued", now) };

    // why: doctor's rule is the one rule, so the System card and `sindri doctor` can't disagree (#3430)
    private static RepoHealth Looked(RepoStatus status, RepoHealth health) => health with { NeedsLook = CodeIndexProblems.For(status, health) is not null };
}
