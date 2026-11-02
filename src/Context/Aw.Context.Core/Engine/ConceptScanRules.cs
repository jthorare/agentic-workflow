using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Reads and checks 'context.conceptScan'. Every refusal is SND-CONTEXT-001 with the key path first.</summary>
internal static class ConceptScanRules
{
    private const int MaxThreshold = 10_000_000;

    /// <summary>The settings of a section; a missing section or key is its default.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>The settings.</returns>
    internal static ConceptScanSettings Read(ConceptScanSection? section) => new()
    {
        Tiers = Tiers(section),
    };

    private static ConceptScanTiers Tiers(ConceptScanSection? section)
    {
        var defaults = ConceptScanTiers.Default;
        return new ConceptScanTiers(section?.StrongAboveFeatures ?? defaults.StrongAboveFeatures, section?.StrongAbovePromptTokens ?? defaults.StrongAbovePromptTokens);
    }

    /// <summary>The first value out of range, or null.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>SND-CONTEXT-001 naming the key path, or null.</returns>
    internal static Error? Problem(ConceptScanSection? section)
    {
        if (section is null)
        {
            return null;
        }

        return section.Models is not null
            ? SettingsReader.Invalid("context.conceptScan.models", "was replaced by context.models.names (claude is claude-code there); move the model names there")
            : Threshold("strongAboveFeatures", section.StrongAboveFeatures) ?? Threshold("strongAbovePromptTokens", section.StrongAbovePromptTokens);
    }

    private static Error? Threshold(string key, int? value) =>
        value is < 1 or > MaxThreshold ? SettingsReader.Invalid("context.conceptScan." + key, "is a whole number from 1 to 10000000") : null;
}
