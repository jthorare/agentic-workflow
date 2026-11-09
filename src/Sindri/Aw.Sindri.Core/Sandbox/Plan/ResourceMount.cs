namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>One resource folder or file a run may write: a database or disk of its owner (PR 14). It becomes a <see cref="PlanMountKind.State"/> mount.</summary>
/// <param name="Name">The resource's name, shown as the mount's reason.</param>
/// <param name="HostPath">The absolute host path, bound read-write at the same path inside the sandbox.</param>
public sealed record ResourceMount(string Name, string HostPath);
