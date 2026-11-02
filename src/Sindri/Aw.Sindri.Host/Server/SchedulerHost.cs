using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RecordServiceRun;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Runs the background services the approved profile names, and the build's built-in services (engine spec §7.2 S1-D8),
/// on their cadence (factory addendum §17.29.1). It re-reads the approval once a second, so an approval takes effect
/// without a restart; a failed run is logged and the next slot still runs, and an approval it can't read is logged once
/// (SND-SCHED-001) while the current schedule keeps running. Each service runs on its own loop (S1-D10): a run still in
/// flight holds back no other service, and its own slots that come due meanwhile are skipped, never overlapped. Nothing
/// runs while the process holds no lease; a stop waits for the runs in flight, which see the stop's cancellation. Each
/// run is its own caller, 'service:&lt;name&gt;', so its events carry that actor (API spec §2.1). A run is recorded as
/// 'service.ran' (C8) when it failed, did work, or is the first ok after a failure; an idle run after an ok one writes
/// nothing. Each service's last recorded outcome is kept in memory and, after a restart, read from its latest row.
/// </summary>
internal sealed partial class SchedulerHost(
    IServiceScopeFactory scopes, ScheduleInputs inputs, ServiceRunGuard guard, HeldLease held, ICallerContextAccessor callers, TimeProvider clock, ILogger<SchedulerHost> log)
    : BackgroundService
{
    private static readonly TimeSpan _tick = TimeSpan.FromSeconds(1);
    private readonly Dictionary<ServiceName, Task> _running = [];
    private IReadOnlyList<ServiceSchedule> _schedules = [];
    private LoadedProfile? _approved;
    private bool _loaded;
    private string? _problem;
    private bool _pausesKnown;   // until the first good read of the pauses no profile service runs (a built-in can't be paused): a restart mid-rebuild must not run what a human paused
    private bool _pausesFailing;   // the last read of the pauses failed (logged once for the stretch)
    private IReadOnlySet<ServiceName> _paused = new HashSet<ServiceName>();   // read once per tick; a failed read keeps the last known set

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Refresh(stoppingToken).ConfigureAwait(false);
            var grant = held.Current;
            if (grant is not null)
            {
                await RunDue(grant, stoppingToken).ConfigureAwait(false);
            }

            var next = grant is null || _schedules.Count == 0 ? _tick : _schedules.Min(s => s.NextSlot) - clock.GetUtcNow();
            await Delay(next < TimeSpan.Zero ? TimeSpan.Zero : next > _tick ? _tick : next, stoppingToken).ConfigureAwait(false);
        }

        await InFlight().ConfigureAwait(false);
    }

    /// <summary>Waits for the runs still in flight; they see the stop's cancellation.</summary>
    private async Task InFlight()
    {
        try
        {
            await Task.WhenAll(_running.Values).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // a run the stop cancelled
        }
    }

    private async Task Refresh(CancellationToken ct)
    {
        Result<LoadedProfile?> approved;
        try
        {
            approved = await inputs.Approvals.Latest(ct).ConfigureAwait(false);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: a broken read (its own timeout too) must not stop the schedule
        {
            approved = SindriErrors.ApprovalsUnavailable.With(e.Message);
        }

        if (approved.IsError)
        {
            if (!string.Equals(_problem, approved.Error.Message, StringComparison.Ordinal))
            {
                _problem = approved.Error.Message;
                LogRefreshFailed(log, approved.Error.Code, approved.Error.Message);
            }

            return;
        }

        _problem = null;
        var profile = approved.Value;
        if (!_loaded || profile?.Hash != _approved?.Hash)
        {
            _loaded = true;   // the first read loads the built-ins even with no approved profile (S1-D8)
            _approved = profile;
            _schedules = ServiceSchedule.From(profile?.Profile, inputs.BuiltIns, clock.GetUtcNow(), await LastRuns(ct).ConfigureAwait(false));
            inputs.Live.Publish(_schedules);
            LogSchedule(log, _approved?.Hash.Abbreviated ?? "none", _schedules.Count);
        }
    }

    private async Task RunDue(LeaseGrant grant, CancellationToken ct)
    {
        await ReadPauses(ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var advanced = new List<ServiceSchedule>(_schedules.Count);
        foreach (var schedule in _schedules)
        {
            if (schedule.NextSlot > now)
            {
                advanced.Add(schedule);
                continue;
            }

            // a paused service's slot passes unrun (so a resume doesn't burst); a service with a run in flight, scheduled or manual, skips this slot (S1-D10)
            IDisposable? claim = null;
            try
            {
                if ((_pausesKnown || IsBuiltIn(schedule.Name)) && !_paused.Contains(schedule.Name) && guard.TryClaim(schedule.Name, out claim))
                {
                    var run = RunOne(schedule, new ScheduledRun(grant, schedule.NextSlot), claim, ct);   // its own loop: not awaited here; it owns the claim now
                    claim = null;
                    if (run.IsCompleted)
                    {
                        await run.ConfigureAwait(false);
                        _ = _running.Remove(schedule.Name);
                    }
                    else
                    {
                        _running[schedule.Name] = run;
                    }
                }
            }
            finally
            {
                claim?.Dispose();
            }

            advanced.Add(schedule.Advance(now));
        }

        inputs.Live.Publish(_schedules = advanced);   // the services listing reads these: without the lease nothing advances, so a slot left in the past was missed
    }

    /// <summary>A built-in service can't be paused (only the profile's can), so it needs no read of the pauses to start.</summary>
    private bool IsBuiltIn(ServiceName name) => BuiltInService.Is(name, _approved?.Profile.Services?.ContainsKey(name.Value) == true, inputs.BuiltIns);

    private async Task ReadPauses(CancellationToken ct)
    {
        try
        {
            var paused = await inputs.Pauses.Paused(ct).ConfigureAwait(false);
            if (paused.IsSuccess)
            {
                _paused = paused.Value;
                _pausesKnown = true;
                _pausesFailing = false;
            }
            else if (!_pausesFailing)
            {
                _pausesFailing = true;   // one line per failing stretch: a rebuild can last minutes, a tick a second
                LogPausesFailed(log, $"{paused.Error.Code} {paused.Error.Message}");
            }
        }
        catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: an unreadable view must not stop the schedule; the last known pauses stand
        {
            LogPausesFailed(log, e.Message);
        }
    }

    private async Task RunOne(ServiceSchedule schedule, ScheduledRun run, IDisposable claim, CancellationToken ct)
    {
        using var release = claim;
        using var caller = callers.EnterService(schedule.Name.Value);
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var outcome = await Outcome(scope.ServiceProvider, schedule.Name, run, ct).ConfigureAwait(false);
            if (await Skip(scope.ServiceProvider, schedule.Name, outcome, ct).ConfigureAwait(false))
            {
                return;
            }

            var recorded = await Record(scope.ServiceProvider, new RecordServiceRunRequest(schedule.Name, run, outcome.Map(_ => Unit.Value)), ct).ConfigureAwait(false);
            if (recorded)
            {
                guard.Record(schedule.Name, outcome.IsError ? ServiceOutcome.Failed : ServiceOutcome.Ok);
            }
            else
            {
                guard.Forget(schedule.Name);   // what the ledger holds is unknown now; read it back if the next run is idle
            }
        }
    }

    /// <summary>Runs the service and returns how it ended: its result, a throw as SND-SCHED-001, or SND-SVC-404 when this build lacks it (G8).</summary>
    private async Task<Result<ScheduledResult>> Outcome(IServiceProvider services, ServiceName name, ScheduledRun run, CancellationToken ct)
    {
        var service = services.GetServices<IScheduledService>().FirstOrDefault(s => s.Identity() == name);
        if (service is null)
        {
            LogUnknownService(log, name.Value);
            return SindriErrors.ServiceNotFound.With($"the profile names service '{name.Value}', which this build doesn't have");
        }

        try
        {
            var result = await service.Run(run, ct).ConfigureAwait(false);
            if (result.IsError)
            {
                LogServiceFailed(log, name.Value, result.Error.Code, result.Error.Message);
            }

            return result;
        }
        catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: one failed run (its own timeout too) must not stop later slots
        {
            var failed = SindriErrors.ServiceFailed.With(e.Message);
            LogServiceFailed(log, name.Value, failed.Code, failed.Message);
            return failed;
        }
    }

    /// <summary>Appends the run's 'service.ran' (API spec C8); a failure to record is logged and the schedule goes on.</summary>
    private async Task<bool> Record(IServiceProvider services, RecordServiceRunRequest request, CancellationToken ct)
    {
        Result<RecordServiceRunResponse> recorded;
        try
        {
            recorded = await services.GetRequiredService<IService<RecordServiceRunRequest, RecordServiceRunResponse>>().Handle(request, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: recording one run must not stop later slots
        {
            recorded = SindriErrors.LedgerUnavailable.With(e.Message);
        }

        if (recorded.IsError)
        {
            LogRecordFailed(log, request.Service.Value, recorded.Error.Code, recorded.Error.Message);
        }

        return recorded.IsSuccess;
    }

    /// <summary>True for an idle run of a service whose last recorded outcome is ok: it writes nothing (C8).</summary>
    private async Task<bool> Skip(IServiceProvider services, ServiceName name, Result<ScheduledResult> outcome, CancellationToken ct)
    {
        if (outcome.IsError || !outcome.Value.Idle)
        {
            return false;
        }

        if (guard.LastOutcome(name) is not { } last)
        {
            var restored = await Restore(services, name, ct).ConfigureAwait(false);
            if (restored is null)
            {
                return false;
            }

            last = restored.Value;
            guard.Record(name, last);
        }

        return last == ServiceOutcome.Ok;
    }

    private async Task Delay(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, clock, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Schedule loaded from approved profile {Profile}: {Count} services")]
    private static partial void LogSchedule(ILogger logger, string profile, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 the approved profile couldn't be read: {Code} {Detail}; keeping the current schedule")]
    private static partial void LogRefreshFailed(ILogger logger, string code, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 the paused services couldn't be read ({Detail}); keeping the last known set")]
    private static partial void LogPausesFailed(ILogger logger, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SVC-404 the profile names service '{Service}', which this build doesn't have")]
    private static partial void LogUnknownService(ILogger logger, string service);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 service '{Service}' ran but its service.ran wasn't recorded: {Code} {Detail}")]
    private static partial void LogRecordFailed(ILogger logger, string service, string code, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 service '{Service}' failed: {Code} {Detail}")]
    private static partial void LogServiceFailed(ILogger logger, string service, string code, string detail);
}
