namespace Aw.Sindri.Core.Reviews;

/// <summary>The risk panel.</summary>
/// <param name="Hits">The risky paths touched.</param>
public sealed record RiskData(IReadOnlyList<ReviewPageRiskHit> Hits) : ReviewPanelData;
