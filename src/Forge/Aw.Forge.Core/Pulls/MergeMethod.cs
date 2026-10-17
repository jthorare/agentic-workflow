namespace Aw.Forge.Core.Pulls;

/// <summary>How the forge merges.</summary>
public enum MergeMethod
{
    /// <summary>A merge commit.</summary>
    Merge = 1,

    /// <summary>Squash into one commit.</summary>
    Squash = 2,

    /// <summary>Rebase the commits.</summary>
    Rebase = 3,
}
