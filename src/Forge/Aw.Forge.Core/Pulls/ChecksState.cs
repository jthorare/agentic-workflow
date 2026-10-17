namespace Aw.Forge.Core.Pulls;

/// <summary>The required checks as one word.</summary>
public enum ChecksState
{
    /// <summary>No check ran for the head.</summary>
    None = 1,

    /// <summary>At least one check hasn't finished.</summary>
    Pending = 2,

    /// <summary>Every check passed.</summary>
    Passing = 3,

    /// <summary>At least one check failed.</summary>
    Failing = 4,
}
