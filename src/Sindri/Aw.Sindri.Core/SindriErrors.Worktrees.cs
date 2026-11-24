using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-WT-001: a worktree request is not valid: the base is not a full 40-hex sha, the repo or path is not absolute, the branch is not usable, or the path is inside another worktree.</summary>
    public static readonly ErrorDefinition WorktreeInvalid = new("SND-WT-001", ErrorKind.Invalid, "The worktree request is not valid");

    /// <summary>SND-WT-002: the worktree path (or its branch) already exists at a different base or branch; nothing is reset, so the engine parks.</summary>
    public static readonly ErrorDefinition WorktreeConflict = new("SND-WT-002", ErrorKind.Conflict, "The worktree path or branch is already in use at a different base");

    /// <summary>SND-WT-003: git failed: the command failed, the repo is missing, or the commit is unknown.</summary>
    public static readonly ErrorDefinition WorktreeGitFailed = new("SND-WT-003", ErrorKind.Unavailable, "git could not create or remove the worktree");

    /// <summary>SND-WT-004: the worktree has uncommitted or untracked files; it is left as it is, never discarded.</summary>
    public static readonly ErrorDefinition WorktreeDirty = new("SND-WT-004", ErrorKind.Conflict, "The worktree has uncommitted changes");
}
