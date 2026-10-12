using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Renews the host lease every 20 s (§17.22 D2). A lost lease (SND-LEASE-002) stops the process: it must not write under
/// a stale epoch. Any other failure (a busy or unreadable ledger) is logged and retried at the next tick, until the grant
/// expires; past expiry the process stops too. It stops renewing once a stop is requested, and a renewal that finishes
/// after a stop cleared the grant, or that the stop cancels in flight, ends quietly: it doesn't bring the grant back, and a release it lost to isn't a loss.
/// </summary>
internal sealed partial class LeaseRenewer(ILease lease, HeldLease held, IShutdown shutdown, IExitCode exit, TimeProvider clock, ILogger<LeaseRenewer> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, shutdown.Stopping);
        while (await Tick(stopping.Token).ConfigureAwait(false))
        {
            if (held.Current is not { } grant)
            {
                continue;
            }

            var renewed = await Renew(grant, stopping.Token).ConfigureAwait(false);
            if (renewed is null || stopping.IsCancellationRequested || !ReferenceEquals(held.Current, grant))
            {
                return;   // a stop cancelled the renewal or cleared the grant while it renewed: not a loss
            }

            if (renewed.IsSuccess)
            {
                _ = held.Replace(grant, renewed.Value);
            }
            else if (renewed.Error.Code != SindriErrors.LeaseLost.Code && clock.GetUtcNow() < grant.ExpiresAt)
            {
                LogRetry(log, renewed.Error.Code, renewed.Error.Message, grant.ExpiresAt);
            }
            else
            {
                LogLost(log, renewed.Error.Code, renewed.Error.Message);
                held.Clear();
                exit.Set(1);
                shutdown.Request();
                return;
            }
        }
    }

    /// <summary>Renews <paramref name="grant"/>; null when a stop cancelled the renewal in flight (a clean end, not a failure).</summary>
    private async Task<Result<LeaseGrant>?> Renew(LeaseGrant grant, CancellationToken stopping)
    {
        try
        {
            return await lease.Renew(grant, stopping).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>Waits one renewal interval; false once stopping.</summary>
    private async Task<bool> Tick(CancellationToken stopping)
    {
        try
        {
            await Task.Delay(LeaseTiming.RenewEvery, clock, stopping).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Code} {Detail}; stopping so nothing is written under a stale epoch")]
    private static partial void LogLost(ILogger logger, string code, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Code} {Detail}; renewing again at the next tick (the grant lasts until {ExpiresAt:O})")]
    private static partial void LogRetry(ILogger logger, string code, string detail, DateTimeOffset expiresAt);
}
