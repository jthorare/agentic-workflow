namespace Aw.Sindri.Core.Setup;

/// <summary>The waits the setup steps use before they call a quiet step stopped.</summary>
public static class SetupTimeouts
{
    /// <summary>
    /// How long the map step may show no new progress line before it counts as stopped. Spec §4.4, the 10-minute guess. Context measures the longest gap between map progress lines (#3316, gap 6),
    /// and this becomes that gap x 2, rounded up to a whole minute, floor 10. No state uses it yet: the stopped state is context's map-step work (C1/P6).
    /// </summary>
    public static TimeSpan MapStoppedAfter { get; } = TimeSpan.FromMinutes(10);
}
