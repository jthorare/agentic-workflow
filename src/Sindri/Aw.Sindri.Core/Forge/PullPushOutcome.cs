namespace Aw.Sindri.Core.Forge;

/// <summary>How a push ended, when git itself worked.</summary>
public enum PullPushOutcome
{
    /// <summary>The branch now points at the commit.</summary>
    Pushed = 1,

    /// <summary>The lease failed: the branch moved since the caller looked. Nothing changed.</summary>
    HeadMoved = 2,

    /// <summary>The remote refused the push for another reason (a protected branch, a hook). Nothing changed.</summary>
    Rejected = 3,
}
