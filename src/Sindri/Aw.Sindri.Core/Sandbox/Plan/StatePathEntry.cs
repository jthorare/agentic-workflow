namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>One exception inside the denied state directory (sandbox spec §3.1): a closed inventory taken from the installed hooks.</summary>
/// <param name="Path">The path relative to AW_STATE_DIR.</param>
/// <param name="Kind">How it appears.</param>
/// <param name="UsedBy">The hooks or tools that use it.</param>
public sealed record StatePathEntry(string Path, StatePathKind Kind, string UsedBy);
