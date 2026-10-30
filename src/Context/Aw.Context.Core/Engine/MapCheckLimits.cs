namespace Aw.Context.Core.Engine;

/// <summary>
/// The limits of the map structure checks, set by 'context.checks' in the profile (map structure spec §4): which leaf home features are measured for isolation, and the shares that make a map degraded or refused.
/// Percentages are whole percent. The file cap is not here: it is what home features split at (<see cref="Extract.HomeFeatures.SizeCap"/>), so one number drives both.
/// </summary>
public sealed record MapCheckLimits
{
    /// <summary>The fewest files a feature needs to be measured for isolation (default 5).</summary>
    public int MinFilesIsolated { get; init; } = 5;

    /// <summary>The fewest such features a map needs for the isolation measure to apply (default 20).</summary>
    public int MinFeaturesIsolated { get; init; } = 20;

    /// <summary>
    /// Isolated features above this percent make the map degraded (default 5). Measured (issue 1365): healthy maps sit at 1 to 1.5%, a TypeScript monorepo that
    /// loses its cross-package imports (a path alias the resolver can't follow) at about 9%, and one that loses every import at 87 to 97%.
    /// </summary>
    public int IsolatedDegradedPercent { get; init; } = 5;

    /// <summary>Isolated features above this percent refuse the map (default 60).</summary>
    public int IsolatedRefusePercent { get; init; } = 60;

    /// <summary>Features over the cap above this percent make the map degraded (default 2).</summary>
    public int OverCapDegradedPercent { get; init; } = 2;

    /// <summary>Features over the cap above this percent refuse the map (default 10).</summary>
    public int OverCapRefusePercent { get; init; } = 10;
}
