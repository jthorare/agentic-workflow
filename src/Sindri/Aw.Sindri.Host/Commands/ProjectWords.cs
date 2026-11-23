using Aw.Sindri.Core.Projects.Truth;

namespace Aw.Sindri.Host.Commands;

/// <summary>The words the project verbs print for a truth's health level and PR state.</summary>
internal static class ProjectWords
{
    internal static string LevelWord(ProjectTruthLevel level) => level switch
    {
        ProjectTruthLevel.OnTrack => "on-track",
        ProjectTruthLevel.AtRisk => "at-risk",
        ProjectTruthLevel.OffTrack => "off-track",
        ProjectTruthLevel.Unknown => "not-checked",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "not a health level"),
    };

    internal static string PrWord(ProjectPrState state) => state switch
    {
        ProjectPrState.Draft => "draft",
        ProjectPrState.Ready => "ready",
        ProjectPrState.CiRed => "ciRed",
        ProjectPrState.Unknown => "unknown",
        ProjectPrState.Waiting => "waiting",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "not a PR state"),
    };
}
