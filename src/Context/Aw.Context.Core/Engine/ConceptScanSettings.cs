namespace Aw.Context.Core.Engine;

/// <summary>'context.conceptScan': when the scan needs the strong tier. A missing key is its default; the models for each tier are 'context.models.names'.</summary>
public sealed record ConceptScanSettings
{
    /// <summary>'context.conceptScan.strongAboveFeatures' and '.strongAbovePromptTokens'.</summary>
    public ConceptScanTiers Tiers { get; init; } = ConceptScanTiers.Default;
}
