namespace Aw.Context.Core.Engine;

/// <summary>'context.conceptScan': when the scan needs the strong tier, and which model each agent CLI runs for each tier. A missing key is its default.</summary>
public sealed record ConceptScanSettings
{
    /// <summary>'context.conceptScan.strongAboveFeatures' and '.strongAbovePromptTokens'.</summary>
    public ConceptScanTiers Tiers { get; init; } = ConceptScanTiers.Default;

    /// <summary>'context.conceptScan.models.&lt;claude|codex|cursor&gt;.&lt;cheap|strong&gt;'.</summary>
    public ConceptScanModels Models { get; init; } = ConceptScanModels.Default;
}
