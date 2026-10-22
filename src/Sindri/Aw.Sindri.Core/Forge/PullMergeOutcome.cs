namespace Aw.Sindri.Core.Forge;

/// <summary>How a merge ended.</summary>
/// <param name="Status">Merged, blocked or head moved.</param>
/// <param name="MergedSha">The merge commit, when merged.</param>
/// <param name="Reason">Why it is blocked (the forge's words, untrusted), or null.</param>
public sealed record PullMergeOutcome(PullMergeStatus Status, string? MergedSha, string? Reason);
