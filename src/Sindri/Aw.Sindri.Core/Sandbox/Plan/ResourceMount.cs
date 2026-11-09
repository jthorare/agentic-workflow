namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>A resource folder a run may write: its owner's folder, which holds the owner's databases and disks (PR 14). It becomes a <see cref="PlanMountKind.State"/> mount.</summary>
/// <param name="Name">A short label for the mount's reason.</param>
/// <param name="HostPath">The absolute host path, bound read-write at the same path inside the sandbox.</param>
public sealed record ResourceMount(string Name, string HostPath);
