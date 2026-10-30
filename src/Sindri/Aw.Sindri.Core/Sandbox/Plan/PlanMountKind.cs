namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>How one entry of the mount stack appears in the sandbox (sandbox spec §3.1).</summary>
public enum PlanMountKind
{
    /// <summary>Bound read-only.</summary>
    ReadOnly,

    /// <summary>Bound read-write.</summary>
    ReadWrite,

    /// <summary>The path does not exist for the session (absent, or masked with an empty file).</summary>
    Deny,

    /// <summary>A sized tmpfs.</summary>
    Tmpfs,

    /// <summary>A per-launch directory, bound read-write at the path, thrown away with the run.</summary>
    PerLaunchDir,

    /// <summary>A fresh /dev.</summary>
    Dev,

    /// <summary>A fresh /proc.</summary>
    Proc,

    /// <summary>A symbolic link the run-area preparer makes at the path, to the target in <see cref="PlanMount.Source"/> (Seatbelt only: it has no bind mounts, so what the agent expects at a fixed path is a link to the real one).</summary>
    Link,
}
