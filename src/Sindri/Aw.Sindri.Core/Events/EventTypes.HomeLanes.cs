namespace Aw.Sindri.Core.Events;

public static partial class EventTypes
{
    /// <summary>A lane trigger of Home holds (lane at its recycle mark, agent limited, PR queued to merge); projects a needs-you row (Home spec §3.3).</summary>
    public const string HomeLaneRowRaised = "home.lane-row.raised";

    /// <summary>A lane trigger of Home stopped holding; clears its needs-you row.</summary>
    public const string HomeLaneRowCleared = "home.lane-row.cleared";
}
