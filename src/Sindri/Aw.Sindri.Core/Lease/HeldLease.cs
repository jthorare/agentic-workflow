namespace Aw.Sindri.Core.Lease;

/// <summary>The grant this process holds, if any: set when the control plane starts, updated on renewal, cleared on stop or loss.</summary>
public sealed class HeldLease
{
    private LeaseGrant? _grant;

    /// <summary>The current grant, or null.</summary>
    public LeaseGrant? Current => Volatile.Read(ref _grant);

    /// <summary>Records <paramref name="grant"/> as held.</summary>
    /// <param name="grant">The grant.</param>
    public void Hold(LeaseGrant grant) => Volatile.Write(ref _grant, grant);

    /// <summary>
    /// Holds <paramref name="next"/> only if the current grant is still <paramref name="expected"/>, so a renewal that
    /// finishes after a stop cleared the grant doesn't bring it back.
    /// </summary>
    /// <param name="expected">The grant the caller read.</param>
    /// <param name="next">The grant to hold instead.</param>
    /// <returns>True when it was replaced.</returns>
    public bool Replace(LeaseGrant expected, LeaseGrant next) => ReferenceEquals(Interlocked.CompareExchange(ref _grant, next, expected), expected);

    /// <summary>Forgets the grant (released or lost).</summary>
    public void Clear() => Volatile.Write(ref _grant, null);
}
