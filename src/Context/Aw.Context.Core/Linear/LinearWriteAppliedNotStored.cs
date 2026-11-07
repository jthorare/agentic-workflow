namespace Aw.Context.Core.Linear;

/// <summary>Linear applied the write but the answer was not stored (SND-LINEAR-013, <see cref="LinearHubErrors.NotStored"/>): the next sync stores it.</summary>
/// <param name="Live">The issue as the re-read saw it, or null when the re-read failed too.</param>
public sealed record LinearWriteAppliedNotStored(LinearIssueView? Live) : LinearWriteOutcome;
