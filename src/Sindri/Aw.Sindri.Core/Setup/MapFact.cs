namespace Aw.Sindri.Core.Setup;

/// <summary>Where the context map stands, from the engine's stored state and the build jobs.</summary>
/// <param name="Phase">The phase.</param>
/// <param name="Progress">How far a running build is, when its progress lines say so; else null.</param>
/// <param name="FailureCode">The error code of the failed build, or null.</param>
public sealed record MapFact(MapPhase Phase, SetupProgress? Progress = null, string? FailureCode = null)
{
    /// <summary>One line per repo of the approved profile, in the state the map is in.</summary>
    public SetupItems Items { get; init; } = SetupItems.None;

    /// <summary>The wait and the reason of a build that failed once and will try again, else null.</summary>
    public MapRetry? Retry { get; init; }
}
