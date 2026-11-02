using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context-sync-gaps' background service (scoping spec C5, owner decision 2026-10-18 "allow, show the gap, queue or start the sync"): when a configured intent source has never synced
/// successfully it starts a sync job as 'service:context-sync-gaps', through the SyncSources command (gate, audit, authorization and 'command.handled'; never the runner). Reads stay pure: the lenses
/// and the history only show the gap. A running sync is never doubled (key 'sync': SND-JOB-409 with 'runningJob' is a quiet run; any other 409 is logged and not retried). It re-checks on the scheduler tick;
/// the scheduler has no event trigger (sindri-api accepted this). A tick reads the sources' stored sync state and nothing else (no live probe, no job read, no command, nothing logged above debug)
/// until a source is a gap; then it reads the sync jobs once, backs off after every ended sync while the gap remains (a source whose sync failed, or that is down and left the job Succeeded with a coded row, is still a gap) and starts one when due.
/// A failure to start surfaces as a failed service run (needs-you 'failure:service:context-sync-gaps').
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine: its stored-state gap check.</param>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="start">The SyncSources command chain.</param>
/// <param name="clock">Dates the backoff.</param>
/// <param name="logger">Logs a 409 that is not "the sync is already running".</param>
internal sealed partial class ContextSyncGapsService(ContextScopes scopes, IContextEngine engine, IJobViews jobs, IService<SyncContextSourcesRequest, JobStartedResponse> start, TimeProvider clock, ILogger<ContextSyncGapsService> logger) : IScheduledService
{
    // The wait caps at 60 minutes from the fifth failure in a row (SyncGapDecision.Wait), so the newest few runs decide it.
    private const int RecentRuns = 8;

    internal static ServiceName ServiceId { get; } = ServiceName.From("context-sync-gaps");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Starts a sync for a context source that has never synced.";

    /// <summary>Runs without a profile entry, every minute: a tick is one stored-state read when nothing is missing.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value.Settings is null)
        {
            // no approved profile is nothing to sync, not a failed run (a failed run would land in needs-you every tick)
            return scope.IsError && scope.Error.Code != SindriErrors.ProfileNotApproved.Code ? scope.Error : ScheduledResult.Quiet;
        }

        var gap = await engine.HasSyncGap(scope.Value, cancellationToken).ConfigureAwait(false);
        if (gap.IsError || !gap.Value)
        {
            return gap.IsError ? gap.Error : ScheduledResult.Quiet;
        }

        return await StartWhenDue(cancellationToken).ConfigureAwait(false);
    }

    // A gap tick: one read of the sync jobs, newest first, then the pure decision.
    private async ValueTask<Result<ScheduledResult>> StartWhenDue(CancellationToken cancellationToken)
    {
        var recent = await jobs.List(state: null, SyncGaps.SyncKind, cancellationToken).ConfigureAwait(false);
        if (recent.IsError)
        {
            return recent.Error;
        }

        var running = recent.Value.Any(static j => j.State == JobState.Running);
        // Only called while a source has no successful sync, so every ended run counts as a failed attempt for it; the job's own state is left as it is (other readers use it).
        IReadOnlyList<SyncAttempt> finished = [.. recent.Value.Where(static j => j.State != JobState.Running && j.EndedAt is not null).Take(RecentRuns)
            .Select(static j => new SyncAttempt(Failed: true, j.EndedAt ?? default))];   // the gap is still there, so no run left the source synced, whatever state the job ended in (a down source ends the job Succeeded with a coded row)
        return SyncGapDecision.Decide(gap: true, running, finished, clock.GetUtcNow()) == SyncGapAction.Start
            ? await Start(cancellationToken).ConfigureAwait(false)
            : ScheduledResult.Quiet;
    }

    private async ValueTask<Result<ScheduledResult>> Start(CancellationToken cancellationToken)
    {
        var started = await start.Handle(new SyncContextSourcesRequest(), cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            return started.Error.Code != SindriErrors.JobConflict.Code ? started.Error : Conflict(started.Error);
        }

        return new ScheduledResult(Idle: false, $"ok: sync {started.Value.JobId.Value} started");
    }

    // 409 with 'runningJob' is the same sync running: nothing to say. Any other 409 is logged, and not retried this tick.
    private ScheduledResult Conflict(Error error)
    {
        if (error.Extensions is null || !error.Extensions.Any(static e => e.Key == "runningJob"))
        {
            LogConflict(logger, error.Code, error.Message);
        }

        return ScheduledResult.Quiet;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "context-sync-gaps: the sync was refused with {Code} and is not retried this tick: {Detail}")]
    private static partial void LogConflict(ILogger logger, string code, string? detail);
}
