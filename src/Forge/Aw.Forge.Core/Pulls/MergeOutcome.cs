namespace Aw.Forge.Core.Pulls;

/// <summary>What a merge call did.</summary>
/// <param name="Status">The result.</param>
/// <param name="MergedSha">The merge commit when merged.</param>
/// <param name="Reason">The forge's reason when blocked (plain text, never logged).</param>
public sealed record MergeOutcome(MergeStatus Status, string? MergedSha, string? Reason);
