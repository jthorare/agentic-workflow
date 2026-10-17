namespace Aw.Forge.Core.Pulls;

/// <summary>The result of a merge call.</summary>
public enum MergeStatus
{
    /// <summary>Merged.</summary>
    Merged = 1,

    /// <summary>The forge won't merge it (checks, reviews, conflicts, closed).</summary>
    Blocked = 2,

    /// <summary>The head isn't the expected one; nothing merged.</summary>
    HeadMoved = 3,
}
