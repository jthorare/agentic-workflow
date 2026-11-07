namespace Aw.Sindri.Core.Reviews;

/// <summary>The evidence panel.</summary>
/// <param name="Items">What was found.</param>
public sealed record EvidenceData(IReadOnlyList<ReviewPageEvidenceItem> Items) : ReviewPanelData;
