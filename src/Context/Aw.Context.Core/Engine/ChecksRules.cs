using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Reads and checks 'context.checks'. Every refusal is SND-CONTEXT-001 with the key path first.</summary>
internal static class ChecksRules
{
    /// <summary>The mode of a section: 'refuse' (default) or 'warn'.</summary>
    /// <param name="written">The mode as written, or null.</param>
    /// <returns>The mode, or null when it is neither.</returns>
    internal static ChecksMode? ModeOf(string? written) => written switch
    {
        null or "refuse" => ChecksMode.Refuse,
        "warn" => ChecksMode.Warn,
        _ => null,
    };

    /// <summary>The mode of a section; refuse when unset.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>The mode.</returns>
    internal static ChecksMode ModeOf(ChecksSection? section) => ModeOf(section?.Mode) ?? ChecksMode.Refuse;

    /// <summary>The limits of a section; a missing section or key is its default.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>The limits.</returns>
    internal static MapCheckLimits Limits(ChecksSection? section) => OverCap(section?.OverCap, Isolated(section?.Isolated, new MapCheckLimits()));

    private static MapCheckLimits Isolated(ChecksIsolatedSection? s, MapCheckLimits d) => d with
    {
        MinFilesIsolated = s?.MinFiles ?? d.MinFilesIsolated,
        MinFeaturesIsolated = s?.MinFeatures ?? d.MinFeaturesIsolated,
        IsolatedDegradedPercent = s?.DegradedPercent ?? d.IsolatedDegradedPercent,
        IsolatedRefusePercent = s?.RefusePercent ?? d.IsolatedRefusePercent,
    };

    private static MapCheckLimits OverCap(ChecksOverCapSection? s, MapCheckLimits d) => d with
    {
        OverCapDegradedPercent = s?.DegradedPercent ?? d.OverCapDegradedPercent,
        OverCapRefusePercent = s?.RefusePercent ?? d.OverCapRefusePercent,
    };

    /// <summary>The first value out of range, or null.</summary>
    /// <param name="section">The section as written, or null.</param>
    /// <returns>SND-CONTEXT-001 naming the key path, or null.</returns>
    internal static Error? Problem(ChecksSection? section)
    {
        if (section is null)
        {
            return null;
        }

        var l = Limits(section);
        return section switch
        {
            { Mode: { } mode } when ModeOf(mode) is null => SettingsReader.Invalid("context.checks.mode", "is 'refuse' (a map whose structure checks fail is not built) or 'warn' (it is built with a degraded note)"),
            _ => IsolatedProblem(section.Isolated, l) ?? OverCapProblem(section.OverCap, l),
        };
    }

    private static Error? IsolatedProblem(ChecksIsolatedSection? s, MapCheckLimits l) => s switch
    {
        { MinFiles: < 1 or > 1_000 } => SettingsReader.Invalid("context.checks.isolated.minFiles", "is a whole number from 1 to 1000"),
        { MinFeatures: < 1 or > 100_000 } => SettingsReader.Invalid("context.checks.isolated.minFeatures", "is a whole number from 1 to 100000"),
        { DegradedPercent: < 0 or > 100 } => SettingsReader.Invalid("context.checks.isolated.degradedPercent", "is a whole percent from 0 to 100"),
        { RefusePercent: < 0 or > 100 } => SettingsReader.Invalid("context.checks.isolated.refusePercent", "is a whole percent from 0 to 100"),
        _ => l.IsolatedDegradedPercent > l.IsolatedRefusePercent ? SettingsReader.Invalid("context.checks.isolated.degradedPercent", "is at most refusePercent (the degraded limit comes first)") : null,
    };

    private static Error? OverCapProblem(ChecksOverCapSection? s, MapCheckLimits l) => s switch
    {
        { DegradedPercent: < 0 or > 100 } => SettingsReader.Invalid("context.checks.overCap.degradedPercent", "is a whole percent from 0 to 100"),
        { RefusePercent: < 0 or > 100 } => SettingsReader.Invalid("context.checks.overCap.refusePercent", "is a whole percent from 0 to 100"),
        _ => l.OverCapDegradedPercent > l.OverCapRefusePercent ? SettingsReader.Invalid("context.checks.overCap.degradedPercent", "is at most refusePercent (the degraded limit comes first)") : null,
    };
}
