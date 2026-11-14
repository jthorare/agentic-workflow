using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// The session collector (collector spec CO2, CI-4): while the process holds the lease it runs one pass every poll interval (2 s by default), keeps the snapshot in memory
/// and appends 'fleet.changed' for the changes worth a row, as the caller 'service:session-collector'. It runs only with the lease, like the profile watch, and never
/// writes into an agent's own folders. A failed pass is logged once per distinct problem and retried on the next tick.
/// </summary>
internal sealed partial class SessionCollectorHost(
    IServiceScopeFactory scopes, HeldLease held, ICallerContextAccessor callers, SessionSettings settings, ISessionSnapshot snapshot, TimeProvider clock, ILogger<SessionCollectorHost> log)
    : BackgroundService
{
    private static readonly TimeSpan _idle = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _budget = TimeSpan.FromMilliseconds(500);
    private bool _restored;
    private string? _problem;
    private string? _problemCodes;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        CollectorTelemetry.Observe(snapshot);
        var every = TimeSpan.FromSeconds(Math.Clamp(settings.PollSeconds, 1, 30));
        while (!stoppingToken.IsCancellationRequested)
        {
            var grant = held.Current;
            if (grant is not null)
            {
                await Pass(grant, stoppingToken).ConfigureAwait(false);
            }

            await Delay(grant is null ? _idle : every, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task Pass(LeaseGrant grant, CancellationToken ct)
    {
        using var caller = callers.EnterService("session-collector");
        using var span = CollectorTelemetry.Source.StartActivity("sindri.collector.pass");
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            try
            {
                await Run(scope.ServiceProvider, grant, ct).ConfigureAwait(false);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: one failed pass must not stop the collector
            {
                Report(Core.SindriErrors.ServiceFailed.With(e.GetType().Name));
            }
        }

        ReportProblems();
    }

    /// <summary>Logs the snapshot's problem codes (codes only, never text from a process) when they change, so a run's log shows what the snapshot says.</summary>
    private void ReportProblems()
    {
        var codes = string.Join(',', (snapshot.Current?.Problems ?? []).Select(static p => p.Code).Distinct(StringComparer.Ordinal));
        if (!string.Equals(_problemCodes, codes, StringComparison.Ordinal))
        {
            _problemCodes = codes;
            LogProblems(log, snapshot.Current?.Sessions.Count ?? 0, codes.Length == 0 ? "none" : codes);
        }
    }

    private async Task Run(IServiceProvider services, LeaseGrant grant, CancellationToken ct)
    {
        if (!_restored)
        {
            var restore = services.GetRequiredService<IService<RestoreFleetHistoryRequest, RestoreFleetHistoryResponse>>();
            var restored = await restore.Handle(new RestoreFleetHistoryRequest(), ct).ConfigureAwait(false);
            if (restored.IsError)
            {
                Report(restored.Error);
                return;
            }

            _restored = true;
        }

        var pass = await services.GetRequiredService<IService<CollectSessionsRequest, CollectSessionsResponse>>().Handle(new CollectSessionsRequest(_budget), ct).ConfigureAwait(false);
        if (pass.IsError)
        {
            Report(pass.Error);
            return;
        }

        CollectorTelemetry.Pruned.Add(pass.Value.Pruned);
        if (pass.Value.Overran)
        {
            CollectorTelemetry.Overruns.Add(1);
        }

        var recorded = await services.GetRequiredService<IService<RecordFleetChangesRequest, RecordFleetChangesResponse>>()
            .Handle(new RecordFleetChangesRequest(grant, pass.Value.Changes) { StateChanges = pass.Value.StateChanges }, ct).ConfigureAwait(false);
        Report(recorded.IsError ? recorded.Error : null);
    }

    private void Report(Error? error)
    {
        if (error is null)
        {
            _problem = null;
            return;
        }

        if (!string.Equals(_problem, error.Code, StringComparison.Ordinal))
        {
            _problem = error.Code;
            LogFailed(log, error.Code, error.Message);
        }
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

    [LoggerMessage(Level = LogLevel.Information, Message = "session collector: {Sessions} live sessions, problems: {Codes}")]
    private static partial void LogProblems(ILogger logger, int sessions, string codes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 the session collector couldn't finish a pass: {Code} {Detail}")]
    private static partial void LogFailed(ILogger logger, string code, string detail);
}
