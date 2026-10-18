namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>A path the session can't see, with the reason the mount stack and 'explain' give.</summary>
/// <param name="Path">The path.</param>
/// <param name="Reason">Why; starts with 'always blocked' when no setting changes it.</param>
internal sealed record DeniedPath(string Path, string Reason);
