using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Lease;

/// <summary>
/// The lease rules (design spec §9.1, factory addendum §17.22 D2), as pure decisions over the stored row. The adapter
/// reads the row, asks <see cref="Liveness"/> when it needs to, and applies the decision in one write transaction.
/// </summary>
public static class LeaseDecision
{
    /// <summary>How far two readings of one process's start time may differ (Linux derives it from boot time and clock ticks).</summary>
    public static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Whether <paramref name="candidate"/> may take the lease, and with which epoch. A released lease is free. A holder
    /// proven dead loses it at once; one proven alive on this machine keeps it even past expiry (its renewals may only be
    /// late); one whose liveness is unknown (another machine) keeps it until it expires.
    /// </summary>
    /// <param name="row">The stored lease, or null when it was never held.</param>
    /// <param name="candidate">Who asks.</param>
    /// <param name="now">The current time.</param>
    /// <param name="liveness">What is known about the stored holder (the adapter asks <see cref="Liveness"/> for any unreleased row).</param>
    /// <returns>The grant, or SND-LEASE-001 naming the holder.</returns>
    public static Result<LeaseGrant> Acquire(LeaseRow? row, LeaseHolder candidate, DateTimeOffset now, HolderLiveness liveness)
    {
        if (row is null)
        {
            return new LeaseGrant(candidate, 1, now + LeaseTiming.Ttl);
        }

        var kept = !row.Released && liveness switch
        {
            HolderLiveness.Alive => true,
            HolderLiveness.Unknown => row.IsLive(now),
            HolderLiveness.ProvenDead => false,
            _ => false,
        };
        return kept
            ? SindriErrors.LeaseHeld.With(string.Create(CultureInfo.InvariantCulture,
                $"held by {row.Holder} since {row.AcquiredAt:yyyy-MM-ddTHH:mm:ssZ} (epoch {row.Epoch})"))
            : new LeaseGrant(candidate, row.Epoch + 1, now + LeaseTiming.Ttl);
    }

    /// <summary>
    /// Extends <paramref name="grant"/>. It succeeds while the row still names this holder at this epoch, even past expiry
    /// (a process that slept, when nobody took the lease meanwhile, never lost anything to fencing).
    /// </summary>
    /// <param name="row">The stored lease.</param>
    /// <param name="grant">The grant being renewed.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The extended grant, or SND-LEASE-002 when another acquisition (or a release) happened.</returns>
    public static Result<LeaseGrant> Renew(LeaseRow? row, LeaseGrant grant, DateTimeOffset now)
        => Holds(row, grant)
            ? grant with { ExpiresAt = now + LeaseTiming.Ttl }
            : SindriErrors.LeaseLost.With(string.Create(CultureInfo.InvariantCulture,
                $"epoch {grant.Epoch} is no longer held by {grant.Holder}"));

    /// <summary>Whether a write fenced by <paramref name="grant"/> may go ahead: the row names this holder at this epoch and isn't released.</summary>
    /// <param name="row">The stored lease.</param>
    /// <param name="grant">The writer's grant.</param>
    /// <returns>True when the epoch is current.</returns>
    public static bool Holds(LeaseRow? row, LeaseGrant grant)
        => row is { Released: false } && row.Epoch == grant.Epoch && row.Holder == grant.Holder;

    /// <summary>What can be said about <paramref name="holder"/>, from this process's point of view.</summary>
    /// <param name="holder">The stored holder.</param>
    /// <param name="self">This process.</param>
    /// <param name="facts">The system's facts about the holder's pid (only meaningful on the same machine and boot).</param>
    /// <returns>Alive, ProvenDead or Unknown. Start times within <see cref="StartTolerance"/> count as the same process.</returns>
    public static HolderLiveness Liveness(LeaseHolder holder, LeaseHolder self, ProcessFacts facts)
    {
        if (holder.Host != self.Host)
        {
            return HolderLiveness.Unknown;
        }

        if (holder.BootId != self.BootId || !facts.Running)
        {
            return HolderLiveness.ProvenDead;
        }

        return facts.StartedAt is { } started && (started - holder.PidStart).Duration() > StartTolerance ? HolderLiveness.ProvenDead : HolderLiveness.Alive;
    }
}
