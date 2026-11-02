namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The per-run caps on the egress proxy, which runs outside the cgroup (sandbox spec §3.2).</summary>
/// <param name="MaxConnections">Concurrent connections.</param>
/// <param name="MaxBytes">Bytes relayed in both directions over the run.</param>
/// <param name="ConnectionLifetime">The longest one connection may stay open. The run's own length belongs to the wall watchdog, not the proxy.</param>
/// <param name="HeadTimeout">How long a client may take to send its request head.</param>
/// <param name="HalfCloseGrace">The total time the other direction may keep going after one side ended (counted from that moment, not reset by traffic).</param>
internal sealed record EgressCaps(int MaxConnections, long MaxBytes, TimeSpan ConnectionLifetime, TimeSpan HeadTimeout, TimeSpan HalfCloseGrace)
{
    /// <summary>The fixed defaults (profile keys later if wanted).</summary>
    public static EgressCaps Default { get; } = new(64, 2L * 1024 * 1024 * 1024, TimeSpan.FromHours(2), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
}
