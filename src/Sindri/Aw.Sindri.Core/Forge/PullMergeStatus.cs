namespace Aw.Sindri.Core.Forge;

/// <summary>How a merge ended, when the forge answered.</summary>
public enum PullMergeStatus
{
    /// <summary>The pull request merged.</summary>
    Merged = 1,

    /// <summary>The forge's rules block it (a draft, checks, requested changes, conflicts, already merged or closed). Nothing changed.</summary>
    Blocked = 2,

    /// <summary>The head isn't the one the caller expected. Nothing changed.</summary>
    HeadMoved = 3,
}
