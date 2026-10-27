using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Reads and checks 'context.conceptScan'. Every refusal is SND-CONTEXT-001 with the key path first.</summary>
internal static partial class ConceptScanRules
{
    private const int MaxThreshold = 10_000_000;

    /// <summary>The settings of a section; a missing section or key is its default.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>The settings.</returns>
    internal static ConceptScanSettings Read(ConceptScanSection? section) => new()
    {
        Tiers = Tiers(section),
        Models = Models(section?.Models),
    };

    private static ConceptScanTiers Tiers(ConceptScanSection? section)
    {
        var defaults = ConceptScanTiers.Default;
        return new ConceptScanTiers(section?.StrongAboveFeatures ?? defaults.StrongAboveFeatures, section?.StrongAbovePromptTokens ?? defaults.StrongAbovePromptTokens);
    }

    private static ConceptScanModels Models(ConceptScanModelsSection? written)
    {
        var defaults = ConceptScanModels.Default;
        return new ConceptScanModels(Cli(written?.Claude, defaults.Claude), Cli(written?.Codex, defaults.Codex), Cli(written?.Cursor, defaults.Cursor));
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

        (string Key, ConceptScanCliSection? Cli)[] clis = [("claude", section.Models?.Claude), ("codex", section.Models?.Codex), ("cursor", section.Models?.Cursor)];
        return Threshold("strongAboveFeatures", section.StrongAboveFeatures)
            ?? Threshold("strongAbovePromptTokens", section.StrongAbovePromptTokens)
            ?? clis.SelectMany(static c => new[] { Name(c.Key, "cheap", c.Cli?.Cheap), Name(c.Key, "strong", c.Cli?.Strong) }).FirstOrDefault(static e => e is not null);
    }

    private static CliModels Cli(ConceptScanCliSection? written, CliModels defaults) =>
        new(written?.Cheap ?? defaults.Cheap, written?.Strong ?? defaults.Strong);

    private static Error? Threshold(string key, int? value) =>
        value is < 1 or > MaxThreshold ? SettingsReader.Invalid("context.conceptScan." + key, "is a whole number from 1 to 10000000") : null;

    private static Error? Name(string cli, string tier, string? value) =>
        value is null || ModelName.IsMatch(value)
            ? null
            : SettingsReader.Invalid($"context.conceptScan.models.{cli}.{tier}", "is the model name the CLI's --model takes: 1 to 100 letters, digits, '.', '_', ':', '/', '@', '+' or '-', not starting with '-'");

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:/@+-]{0,99}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ModelName { get; }
}
