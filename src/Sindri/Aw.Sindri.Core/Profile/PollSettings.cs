using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The 'workflow.poll' section (#2258, PR 11): how often connections are polled for events that start workflows, and how far a catch-up reads.
/// </summary>
/// <param name="IntervalSeconds">Seconds between two polls of one connection (default 120, at least 30). The service wakes every 30 seconds and polls the connections that are due.</param>
/// <param name="CatchUpPages">The most pages a catch-up reads for one (connection, event kind) before it records catchup.capped (default 20, 1 to 100).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PollSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? IntervalSeconds = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? CatchUpPages = null)
{
    /// <summary>Gets the default poll interval: 120 seconds.</summary>
    public const int DefaultIntervalSeconds = 120;

    /// <summary>Gets the smallest poll interval: 30 seconds.</summary>
    public const int MinIntervalSeconds = 30;

    /// <summary>Gets the default catch-up page cap: 20.</summary>
    public const int DefaultCatchUpPages = 20;

    /// <summary>Gets the largest catch-up page cap: 100.</summary>
    public const int MaxCatchUpPages = 100;

    /// <summary>Reads the interval of a profile's poll section.</summary>
    /// <param name="settings">The section, or null.</param>
    /// <returns>The configured interval, or the default.</returns>
    public static TimeSpan Interval(PollSettings? settings) => TimeSpan.FromSeconds(settings?.IntervalSeconds ?? DefaultIntervalSeconds);

    /// <summary>Reads the catch-up page cap of a profile's poll section.</summary>
    /// <param name="settings">The section, or null.</param>
    /// <returns>The configured cap, or the default.</returns>
    public static int Pages(PollSettings? settings) => settings?.CatchUpPages ?? DefaultCatchUpPages;
}
