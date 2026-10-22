namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>What a workspace rebase did.</summary>
public enum RebaseStatus
{
    /// <summary>The branch moved: it has a new tip.</summary>
    Rebased = 1,

    /// <summary>A commit conflicted; the rebase was aborted and the worktree is as it was.</summary>
    Conflict = 2,

    /// <summary>The branch was already on the base; nothing moved.</summary>
    Nothing = 3,
}
