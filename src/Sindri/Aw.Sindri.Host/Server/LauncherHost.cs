using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Features.Launching;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// The launcher service (UI spec E-1; caller 'service:launcher'): while the process holds the lease it ticks once a second, and each tick hands the new
/// launch requests out. Every request runs on its own task, so a takeover that waits for the engine holds back neither the expiry (a request past its 30 s
/// deadline is failed as SND-LAUNCH-007) nor another request. It runs only with the lease, like the other background services, and a stop waits for the
/// requests in flight, which see its cancellation. A failed tick is logged once per distinct problem and tried again on the next.
/// </summary>
internal sealed partial class LauncherHost(IServiceScopeFactory scopes, HeldLease held, ICallerContextAccessor callers, TimeProvider clock, ILogger<LauncherHost> log)
    : BackgroundService
{
    private static readonly TimeSpan _tick = TimeSpan.FromSeconds(1);
    private readonly List<Task> _running = [];
    private string? _problem;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (held.Current is { } grant)
            {
                await Tick(grant, stoppingToken).ConfigureAwait(false);
            }

            await Delay(stoppingToken).ConfigureAwait(false);
        }

        await InFlight().ConfigureAwait(false);
    }

    private async Task Tick(LeaseGrant grant, CancellationToken ct)
    {
        using var caller = callers.EnterService("launcher");
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            try
            {
                var ticked = await scope.ServiceProvider.GetRequiredService<IService<RunLaunchRequest, RunLaunchResponse>>().Handle(new RunLaunchRequest(grant), ct).ConfigureAwait(false);
                if (ticked.IsError)
                {
                    Report(ticked.Error.Code, ticked.Error.Message);
                    return;
                }

                Report(code: null, detail: null);
                _ = _running.RemoveAll(static t => t.IsCompleted);
                _running.AddRange(ticked.Value.Due.Select(due => Process(grant, due, ct)));
            }
            catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: one failed tick must not stop the launcher
            {
                Report(Core.SindriErrors.ServiceFailed.Code, e.GetType().Name);
            }
        }
    }

    private async Task Process(LeaseGrant grant, PendingLaunch due, CancellationToken ct)
    {
        using var caller = callers.EnterService("launcher");
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            try
            {
                var done = await scope.ServiceProvider.GetRequiredService<IService<ProcessLaunchRequest, ProcessLaunchResponse>>().Handle(new ProcessLaunchRequest(grant, due), ct).ConfigureAwait(false);
                if (done.IsError)
                {
                    Report(done.Error.Code, done.Error.Message);
                }
            }
            catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: a launch that throws is that launch's outcome (the sweep fails it), not the host's
            {
                Report(Core.SindriErrors.ServiceFailed.Code, e.GetType().Name);
            }
        }
    }

    private async Task InFlight()
    {
        try
        {
            await Task.WhenAll(_running).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // a launch the stop cancelled
        }
    }

    private void Report(string? code, string? detail)
    {
        if (code is null)
        {
            _problem = null;
        }
        else if (!string.Equals(_problem, code, StringComparison.Ordinal))
        {
            _problem = code;
            LogFailed(log, code, detail ?? string.Empty);
        }
    }

    private async Task Delay(CancellationToken ct)
    {
        try
        {
            await Task.Delay(_tick, clock, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 the launcher service couldn't finish a tick: {Code} {Detail}")]
    private static partial void LogFailed(ILogger logger, string code, string detail);
}
