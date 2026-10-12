namespace Aw.Sindri.Core.Lease;

/// <summary>What can be said about a lease holder's process.</summary>
public enum HolderLiveness
{
    /// <summary>Its process is running.</summary>
    Alive,

    /// <summary>Its process is provably gone: another boot, no such pid, or a pid reused by a later process.</summary>
    ProvenDead,

    /// <summary>Nothing can be proven (another machine); only expiry frees the lease.</summary>
    Unknown,
}
