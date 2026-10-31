using Aw.Sindri.Core.Lease;
using Aw.Sindri.Features.CheckProfile;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// The profile watch (API spec C8): while the process holds the lease it checks the live profile against the approved
/// one at start and then every 30 s, as the caller 'service:profile-watch', and records 'profile.checked' when the pair
/// changed. A failed check is logged once per distinct problem and retried on the next tick.
/// </summary>
internal sealed class ProfileWatch(ProfileRecheck recheck, HeldLease held, TimeProvider clock) : BackgroundService
{
    /// <summary>How often a check runs while the lease is held.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan _idle = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var holding = held.Current is not null;
            if (holding)
            {
                await recheck.Now(stoppingToken).ConfigureAwait(false);   // as service:profile-watch; a failed check is logged there and retried on the next tick
            }

            await Delay(holding ? Every : _idle, stoppingToken).ConfigureAwait(false);
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
}
