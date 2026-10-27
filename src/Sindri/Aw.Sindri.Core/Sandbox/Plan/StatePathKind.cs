namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>How a state-directory path the hooks use appears in the session.</summary>
public enum StatePathKind
{
    /// <summary>The real path, read-only.</summary>
    ReadOnly,

    /// <summary>The real path, read-write.</summary>
    ReadWrite,

    /// <summary>A per-launch directory bound at the path.</summary>
    PerLaunchDir,

    /// <summary>Not bound: the hook does nothing, by design.</summary>
    Absent,
}
