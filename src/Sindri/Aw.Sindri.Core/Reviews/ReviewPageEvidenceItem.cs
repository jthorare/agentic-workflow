namespace Aw.Sindri.Core.Reviews;

/// <summary>One piece of evidence found for the head.</summary>
/// <param name="Kind">uiEvidence, verificationPack, proofPack or prBody.</param>
/// <param name="Label">Its words.</param>
/// <param name="Path">Where it is.</param>
/// <param name="Url">Its link.</param>
/// <param name="MatchedBy">sha or prNumber.</param>
/// <param name="At">When it was made.</param>
public sealed record ReviewPageEvidenceItem(
    string Kind,
    string Label,
    string Path,
    string? Url,
    string MatchedBy,
    DateTimeOffset At);
