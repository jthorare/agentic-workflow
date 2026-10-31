using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Context's <see cref="IMapRebuildState"/> over Sindri's jobs view and the refresh service's own inputs (concept pins spec §3.7): a refresh queued or running is
/// <see cref="MapRebuildPhase.Building"/>; with none, the first reason the 'context-map-refresh' service would not start one on its next tick: a held map, no repo on its
/// main branch, or the backoff after failed refreshes. Any read that fails answers Idle: the caller then tells the fix, which is never wrong.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">Resolves the engine on use: the engine's query path asks this state, so a constructor dependency would be a cycle.</param>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="clock">Dates the backoff.</param>
internal sealed class MapRebuildState(ContextScopes scopes, Func<IContextEngine> engine, IJobViews jobs, TimeProvider clock) : IMapRebuildState
{
    private const int RecentRuns = 8;

    public async ValueTask<MapRebuildStatus> Read(CancellationToken cancellationToken)
    {
        var refreshes = await jobs.List(state: null, ContextMapRefreshService.RefreshKind, cancellationToken).ConfigureAwait(false);
        if (refreshes.IsError)
        {
            return MapRebuildStatus.Idle;
        }

        if (refreshes.Value.FirstOrDefault(static j => j.State is JobState.Running or JobState.Waiting) is { } running)
        {
            return new MapRebuildStatus(MapRebuildPhase.Building) { Progress = running.Progress.MaxBy(static l => l.Round)?.Line };
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value.Settings is null)
        {
            return MapRebuildStatus.Idle;
        }

        var context = engine();
        var state = await context.Held(scope.Value, cancellationToken).ConfigureAwait(false);
        var heads = await context.MapHeads(scope.Value, cancellationToken).ConfigureAwait(false);
        if (state.IsError || heads.IsError)
        {
            return MapRebuildStatus.Idle;
        }

        if (state.Value.Hold is not null)
        {
            return new MapRebuildStatus(MapRebuildPhase.MapHeld);
        }

        return !heads.Value.Any(static h => h.OnDefault) ? new MapRebuildStatus(MapRebuildPhase.NoMainBranch) : Backoff(refreshes.Value);
    }

    private MapRebuildStatus Backoff(IReadOnlyList<JobRow> refreshes)
    {
        var finished = refreshes.Where(static j => j.State is not (JobState.Running or JobState.Waiting) && j.EndedAt is not null).OrderByDescending(static j => j.EndedAt).Take(RecentRuns).ToArray();
        var failures = MainMoveDecision.FailedInARow([.. finished.Select(static j => new SyncAttempt(j.State == JobState.Failed, j.EndedAt ?? default))]);
        return failures > 0 && finished[0].EndedAt + SyncGapDecision.Wait(failures) is { } retry && clock.GetUtcNow() < retry
            ? new MapRebuildStatus(MapRebuildPhase.Backoff) { RetryAt = retry }
            : MapRebuildStatus.Idle;
    }
}
