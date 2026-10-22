using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context-map-refresh' background service (owner F1, 2026-10-19; the publish gate, owner decision 2026-10-20): one service, two job kinds. A tick, in order: (1) scope; (2) with a held draft, reads the code
/// index's outlooks and asks <see cref="MapPublishDecision"/>; a publish or drop starts 'PublishContextMap' as 'service:context-map-refresh' when none runs and its backoff (5, 10, 20, 40, then 60 minutes over the newest
/// publish failures) isn't holding; (3) the move check: when a mapped repo's checkout moved along its default branch past the heads in <see cref="MappedHeads"/> (the held draft's, else the current version's) it starts a
/// 'RefreshContextMap', never while either kind runs, with the refresh backoff over refresh jobs only; (4) the alarm, the one step that merges the kinds: the newest 8 ended jobs of both give the failure count, and three
/// in a row fail the run (needs-you 'failure:service:context-map-refresh'). A repo on a feature branch is left out and named in the quiet run's summary. No step ends the tick for the later ones.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine: the mapped repos' heads and the stored publish state.</param>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="outlooks">The code index's side of the publish gate.</param>
/// <param name="starts">The RefreshContextMap and PublishContextMap command chains.</param>
/// <param name="clock">Dates the backoff and the wait.</param>
/// <param name="logger">Logs a 409 that is not "the job is already running".</param>
internal sealed partial class ContextMapRefreshService(ContextScopes scopes, IContextEngine engine, IJobViews jobs, IIndexOutlooks outlooks, MapJobStarts starts, TimeProvider clock, ILogger<ContextMapRefreshService> logger) : IScheduledService
{
    // The wait caps at 60 minutes from the fifth failure in a row, and the failure alarm needs three: the newest few runs decide both.
    private const int RecentRuns = 8;
    private const int FailuresToAlarm = 3;

    internal static JobKind RefreshKind { get; } = JobKind.From("RefreshContextMap");

    internal static JobKind PublishKind { get; } = JobKind.From("PublishContextMap");

    internal static ServiceName ServiceId { get; } = ServiceName.From("context-map-refresh");

    /// <summary>Runs without a profile entry, every minute: a tick is one heads read, one state read and two jobs reads.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)));

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value.Settings is null)
        {
            // no approved profile is nothing to refresh, not a failed run (a failed run would land in needs-you every tick)
            return scope.IsError && scope.Error.Code != SindriErrors.ProfileNotApproved.Code ? scope.Error : ScheduledResult.Quiet;
        }

        var tick = await Read(scope.Value, cancellationToken).ConfigureAwait(false);
        return tick.IsError ? tick.Error : await RunTick(scope.Value, tick.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Tick>> Read(ContextScope scope, CancellationToken cancellationToken)
    {
        var heads = await engine.MapHeads(scope, cancellationToken).ConfigureAwait(false);
        var state = await engine.Held(scope, cancellationToken).ConfigureAwait(false);
        var refreshes = await jobs.List(state: null, RefreshKind, cancellationToken).ConfigureAwait(false);
        var publishes = await jobs.List(state: null, PublishKind, cancellationToken).ConfigureAwait(false);
        if (heads.IsError || state.IsError)
        {
            return heads.IsError ? heads.Error : state.Error;
        }

        if (refreshes.IsError || publishes.IsError)
        {
            return refreshes.IsError ? refreshes.Error : publishes.Error;
        }

        return new Tick(heads.Value, state.Value, refreshes.Value, publishes.Value, [.. heads.Value.Where(static h => !h.OnDefault).Select(static h => h.Repo.Value).Order(StringComparer.Ordinal)]);
    }

    private async ValueTask<Result<ScheduledResult>> RunTick(ContextScope scope, Tick tick, CancellationToken cancellationToken)
    {
        var publish = await PublishStep(scope, tick, cancellationToken).ConfigureAwait(false);
        var tail = await MoveAndAlarm(tick, publish.Started, cancellationToken).ConfigureAwait(false);
        if (publish.Error is { } carried)
        {
            return carried;   // the move check and the alarm ran first: a failing outlooks read never freezes a replacing refresh
        }

        if (tail.IsError)
        {
            return tail;
        }

        var notes = new[] { publish.Note, tail.Value.Summary }.Where(static n => n is not null);
        return new ScheduledResult(tail.Value.Idle && !publish.Started, notes.Any() ? string.Join("; ", notes) : null);
    }

    // Step 2: with a hold, ask the gate and start the publish when it says go (or drop) and nothing stops it.
    private async ValueTask<PublishOutcome> PublishStep(ContextScope scope, Tick tick, CancellationToken cancellationToken)
    {
        if (MapGate.HoldOf(tick.State) is not { } hold)
        {
            return default;
        }

        var wait = MapGate.Wait(scope);
        var read = await outlooks.Read(hold, cancellationToken).ConfigureAwait(false);
        if (wait.IsError || read.IsError)
        {
            return new PublishOutcome(Started: false, Note: null, wait.IsError ? wait.Error : read.Error);
        }

        var action = MapPublishDecision.Decide(MapGate.Input(tick.State, hold, read.Value, wait.Value), clock.GetUtcNow());
        if (action is MapWait)
        {
            return new PublishOutcome(Started: false, string.Create(CultureInfo.InvariantCulture, $"map v{hold.Version} held for the code index"), Error: null);
        }

        return Stands(action, tick) ? default : await StartPublish(cancellationToken).ConfigureAwait(false);
    }

    // The refresh that is running replaces a gone draft, and a running publish or its backoff means the start waits.
    private bool Stands(MapPublishAction action, Tick tick) =>
        (action is MapDrop { Reason: MapDropReason.Gone } && tick.Refreshes.Any(static j => j.State == JobState.Running))
        || tick.Publishes.Any(static j => j.State == JobState.Running)
        || PublishBackoffHolds(tick);

    private async ValueTask<PublishOutcome> StartPublish(CancellationToken cancellationToken)
    {
        var started = await starts.Publish(cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            if (started.Error.Code != SindriErrors.JobConflict.Code)
            {
                return new PublishOutcome(Started: false, Note: null, started.Error);
            }

            _ = Conflict(started.Error);   // 409: the same publish is running (quiet), or another refusal that is logged and not retried this tick
            return default;
        }

        return new PublishOutcome(Started: true, $"ok: publish {started.Value.JobId.Value} started", Error: null);
    }

    private bool PublishBackoffHolds(Tick tick)
    {
        var finished = Finished(tick.Publishes).ToArray();
        var failures = MainMoveDecision.FailedInARow(Attempts(finished));
        return failures > 0 && clock.GetUtcNow() < finished[0].EndedAt + SyncGapDecision.Wait(failures);
    }

    // Steps 3 and 4: the move check over the stored map's heads, then the alarm over both kinds.
    private async ValueTask<Result<ScheduledResult>> MoveAndAlarm(Tick tick, bool publishStarted, CancellationToken cancellationToken)
    {
        var onDefault = tick.Heads.Where(static h => h.OnDefault).ToDictionary(static h => h.Repo.Value, static h => h.Head, StringComparer.Ordinal);
        var refreshed = Finished(tick.Refreshes).ToArray();
        var running = publishStarted || tick.Refreshes.Concat(tick.Publishes).Any(static j => j.State == JobState.Running);
        var action = MainMoveDecision.Decide(onDefault, MapGate.Mapped(tick.State), running, Attempts(refreshed), clock.GetUtcNow());
        var both = Finished([.. tick.Refreshes, .. tick.Publishes]).ToArray();
        var failures = MainMoveDecision.FailedInARow(Attempts(both));
        var pending = action != MainMoveAction.Nothing || tick.State.Hold is not null;   // a failing publish keeps the alarm up while its draft is still held
        if (failures < FailuresToAlarm || !pending)
        {
            return action == MainMoveAction.Start
                ? await Start(tick.Elsewhere, cancellationToken).ConfigureAwait(false)
                : new ScheduledResult(Idle: true, Left(tick.Elsewhere));
        }

        return await Alarm(action, failures, both[0].Code, tick.Elsewhere, cancellationToken).ConfigureAwait(false);
    }

    private static JobRow[] Finished(IEnumerable<JobRow> jobs) =>
        [.. jobs.Where(static j => j.State != JobState.Running && j.EndedAt is not null).OrderByDescending(static j => j.EndedAt).Take(RecentRuns)];

    private static IReadOnlyList<SyncAttempt> Attempts(JobRow[] finished) =>
        [.. finished.Select(static j => new SyncAttempt(j.State == JobState.Failed, j.EndedAt ?? default))];

    // The alarm stays up until a job succeeds or nothing is left to do, and the backoff still retries under it.
    private async ValueTask<Result<ScheduledResult>> Alarm(MainMoveAction action, int failures, string? code, string[] elsewhere, CancellationToken cancellationToken)
    {
        var alarm = string.Create(CultureInfo.InvariantCulture, $"{ServiceId.Value}: the last {failures} map refreshes or publishes failed ({code ?? "no code"})");
        if (action != MainMoveAction.Start)
        {
            return SindriErrors.ServiceFailed.With(alarm + "; the next try backs off");
        }

        var retried = await Start(elsewhere, cancellationToken).ConfigureAwait(false);
        return retried.IsError ? retried.Error : SindriErrors.ServiceFailed.With(alarm + "; " + (retried.Value.Summary ?? "the next try backs off"));
    }

    private static string? Left(string[] elsewhere) =>
        elsewhere.Length == 0 ? null : "not on trunk, left out: " + string.Join(", ", elsewhere);

    private async ValueTask<Result<ScheduledResult>> Start(string[] elsewhere, CancellationToken cancellationToken)
    {
        var started = await starts.Refresh(cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            return started.Error.Code != SindriErrors.JobConflict.Code ? started.Error : Conflict(started.Error);
        }

        var note = Left(elsewhere);
        return new ScheduledResult(Idle: false, $"ok: refresh {started.Value.JobId.Value} started{(note is null ? string.Empty : "; " + note)}");
    }

    // 409 with 'runningJob' is the same refresh running: nothing to say. Any other 409 is logged, and not retried this tick.
    private ScheduledResult Conflict(Error error)
    {
        if (error.Extensions is null || !error.Extensions.Any(static e => e.Key == "runningJob"))
        {
            LogConflict(logger, error.Code, error.Message);
        }

        return ScheduledResult.Quiet;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "context-map-refresh: the refresh was refused with {Code} and is not retried this tick: {Detail}")]
    private static partial void LogConflict(ILogger logger, string code, string? detail);

    private sealed record Tick(IReadOnlyList<RepoHead> Heads, MapPublishState State, IReadOnlyList<JobRow> Refreshes, IReadOnlyList<JobRow> Publishes, string[] Elsewhere);

    private readonly record struct PublishOutcome(bool Started, string? Note, Error? Error);
}
