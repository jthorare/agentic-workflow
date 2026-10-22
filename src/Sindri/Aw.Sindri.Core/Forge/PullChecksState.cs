namespace Aw.Sindri.Core.Forge;

/// <summary>The required checks of a commit as one word.</summary>
public enum PullChecksState
{
    /// <summary>No check ran for the commit.</summary>
    None = 1,

    /// <summary>At least one check has not finished.</summary>
    Pending = 2,

    /// <summary>Every check passed.</summary>
    Passing = 3,

    /// <summary>At least one check failed.</summary>
    Failing = 4,
}
