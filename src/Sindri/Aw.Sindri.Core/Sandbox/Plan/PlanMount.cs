namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>One entry of the mount stack, bottom to top: a later entry wins over an earlier one.</summary>
/// <param name="Path">The path inside the sandbox.</param>
/// <param name="Kind">How it appears.</param>
/// <param name="Source">The host path bound there, or null when it is the same path (or nothing); for a link, its target.</param>
/// <param name="Size">The size in bytes of a tmpfs, else null.</param>
/// <param name="Reason">Why the entry exists, in a few words.</param>
/// <param name="Optional">True when the host path may be missing and the entry is then left out (a toolchain directory the box does not have).</param>
/// <param name="PrivateScratch">True for the .NET runtime's private scratch directory: on Seatbelt it becomes a write allowance on its real path, since Seatbelt has no tmpfs (#2089).</param>
public sealed record PlanMount(string Path, PlanMountKind Kind, string? Source = null, long? Size = null, string Reason = "", bool Optional = false, bool PrivateScratch = false);
