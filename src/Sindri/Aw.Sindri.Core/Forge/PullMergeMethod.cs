namespace Aw.Sindri.Core.Forge;

/// <summary>How a pull request merges.</summary>
public enum PullMergeMethod
{
    /// <summary>A merge commit.</summary>
    Merge = 1,

    /// <summary>One squashed commit.</summary>
    Squash = 2,

    /// <summary>The commits rebased onto the base.</summary>
    Rebase = 3,
}
