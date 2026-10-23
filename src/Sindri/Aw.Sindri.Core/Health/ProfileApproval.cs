namespace Aw.Sindri.Core.Health;

/// <summary>Where the profile stands against its approval, as health reads it. A change since approval outranks a missing approval.</summary>
public enum ProfileApproval
{
    /// <summary>Approved and unchanged.</summary>
    Approved,

    /// <summary>The live profile changed since it was approved.</summary>
    Changed,

    /// <summary>No profile is approved.</summary>
    NeedsApproval,
}
