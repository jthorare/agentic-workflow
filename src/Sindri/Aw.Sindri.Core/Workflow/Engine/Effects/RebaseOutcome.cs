namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>How a workspace rebase ended (PB spec §4.6).</summary>
/// <param name="Status">Whether the branch moved, conflicted or was already on the base.</param>
/// <param name="NewHead">The branch's tip after a rebase; null otherwise.</param>
/// <param name="Conflicts">The paths that conflicted, in ordinal order; empty otherwise.</param>
public sealed record RebaseOutcome(RebaseStatus Status, string? NewHead, IReadOnlyList<string> Conflicts);
