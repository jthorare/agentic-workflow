using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Schedule;

/// <summary>The time fields of a schedule trigger's data: the instant in UTC, and the date and weekday in the zone the cron reads (#3569).</summary>
public static class ScheduleStamp
{
    /// <summary>The zone a schedule node's cron reads: its 'timezone' config, or null for UTC.</summary>
    public static string? ZoneOf(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Config["timezone"]?.GetValue<string>();
    }

    /// <summary>Sets scheduledFor (UTC, "O"), localDate (yyyy-MM-dd) and weekday (English name) on <paramref name="trigger"/>.</summary>
    /// <param name="trigger">The trigger data being built.</param>
    /// <param name="at">The tick, or the start time of a hand start.</param>
    /// <param name="timezone">An IANA id, or null for UTC; an unknown id reads as UTC.</param>
    public static void Add(JsonObject trigger, DateTimeOffset at, string? timezone)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var local = TimeZoneInfo.ConvertTime(at, Zone(timezone));
        trigger["scheduledFor"] = at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        trigger["localDate"] = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        trigger["weekday"] = local.ToString("dddd", CultureInfo.InvariantCulture);
    }

    private static TimeZoneInfo Zone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
