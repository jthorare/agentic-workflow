using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Engine;
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
/// The 'context-map-refresh' background service (owner F1, 2026-10-19): when a mapped repo's checkout moves along its default branch it starts a RefreshContextMap job as
/// 'service:context-map-refresh' (the command chain: gate, audit and authorization; never the runner), so the map keeps itself current. A tick reads the mapped repos' heads from the engine
/// and the refresh jobs from the 'jobs' view, then decides in <see cref="MainMoveDecision"/>: a running refresh is never doubled, the heads are compared with the ones the last successful refresh
/// mapped, and failures back off 5, 10, 20, 40, then 60 minutes. A repo on a feature branch is left out and named in the quiet run's summary. Three failed refreshes in a row fail the run
/// (needs-you 'failure:service:context-map-refresh'), while the backoff holds the next start and while its retry runs; a success resets it.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine: the mapped repos' heads.</param>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="start">The RefreshContextMap command chain.</param>
/// <param name="clock">Dates the backoff.</param>
/// <param name="logger">Logs a 409 that is not "the refresh is already running".</param>
internal sealed partial class ContextMapRefreshService(ContextScopes scopes, IContextEngine engine, IJobViews jobs, IService<RefreshContextMapRequest, JobStartedResponse> start, TimeProvider clock, ILogger<ContextMapRefreshService> logger) : IScheduledService
{
    // The wait caps at 60 minutes from the fifth failure in a row, and the failure alarm needs three: the newest few runs decide both.
    private const int RecentRuns = 8;
    private const int FailuresToAlarm = 3;

    internal static JobKind RefreshKind { get; } = JobKind.From("RefreshContextMap");

    internal static ServiceName ServiceId { get; } = ServiceName.From("context-map-refresh");

    /// <summary>Runs without a profile entry, every minute: a tick is one heads read and one jobs read.</summary>
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

        var heads = await engine.MapHeads(scope.Value, cancellationToken).ConfigureAwait(false);
        if (heads.IsError)
        {
            return heads.Error;
        }

        var elsewhere = heads.Value.Where(static h => !h.OnDefault).Select(static h => h.Repo.Value).Order(StringComparer.Ordinal).ToArray();
        var recent = await jobs.List(state: null, RefreshKind, cancellationToken).ConfigureAwait(false);
        return recent.IsError ? recent.Error : await Decide(heads.Value, recent.Value, elsewhere, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ScheduledResult>> Decide(IReadOnlyList<RepoHead> heads, IReadOnlyList<JobRow> recent, string[] elsewhere, CancellationToken cancellationToken)
    {
        var onDefault = heads.Where(static h => h.OnDefault).ToDictionary(static h => h.Repo.Value, static h => h.Head, StringComparer.Ordinal);
        var finishedJobs = recent.Where(static j => j.State != JobState.Running && j.EndedAt is not null).OrderByDescending(static j => j.EndedAt).Take(RecentRuns).ToArray();
        IReadOnlyList<SyncAttempt> finished = [.. finishedJobs.Select(static j => new SyncAttempt(j.State == JobState.Failed, j.EndedAt ?? default))];
        var last = finishedJobs.FirstOrDefault(static j => j.State == JobState.Succeeded);
        var action = MainMoveDecision.Decide(onDefault, last is null ? null : HeadsOf(last), recent.Any(static j => j.State == JobState.Running), finished, clock.GetUtcNow());
        var failures = MainMoveDecision.FailedInARow(finished);
        if (failures < FailuresToAlarm || action == MainMoveAction.Nothing)
        {
            return action == MainMoveAction.Start
                ? await Start(elsewhere, cancellationToken).ConfigureAwait(false)
                : new ScheduledResult(Idle: true, Left(elsewhere));
        }

        return await Alarm(action, failures, finishedJobs[0].Code, elsewhere, cancellationToken).ConfigureAwait(false);
    }

    // The alarm stays up until a refresh succeeds or the heads are back at the last mapped ones, and the backoff still retries under it.
    private async ValueTask<Result<ScheduledResult>> Alarm(MainMoveAction action, int failures, string? code, string[] elsewhere, CancellationToken cancellationToken)
    {
        var alarm = string.Create(CultureInfo.InvariantCulture, $"{ServiceId.Value}: the last {failures} map refreshes failed ({code ?? "no code"})");
        if (action != MainMoveAction.Start)
        {
            return SindriErrors.ServiceFailed.With(alarm + "; the next try backs off");
        }

        var retried = await Start(elsewhere, cancellationToken).ConfigureAwait(false);
        return retried.IsError ? retried.Error : SindriErrors.ServiceFailed.With(alarm + "; " + (retried.Value.Summary ?? "the next try backs off"));
    }

    // The heads the last successful refresh mapped; a result that kept none (or an unreadable one) counts as no heads, so the next tick starts one.
    private static Dictionary<string, string>? HeadsOf(JobRow job)
    {
        if (job.Result is not { ValueKind: JsonValueKind.Object } result || !result.TryGetProperty("heads", out var heads) || heads.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return heads.EnumerateObject().Where(static p => p.Value.ValueKind == JsonValueKind.String).ToDictionary(static p => p.Name, static p => p.Value.GetString() ?? string.Empty, StringComparer.Ordinal);
    }

    private static string? Left(string[] elsewhere) =>
        elsewhere.Length == 0 ? null : "not on trunk, left out: " + string.Join(", ", elsewhere);

    private async ValueTask<Result<ScheduledResult>> Start(string[] elsewhere, CancellationToken cancellationToken)
    {
        var started = await start.Handle(new RefreshContextMapRequest(), cancellationToken).ConfigureAwait(false);
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
}
