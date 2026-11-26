using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Schedule;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>A workflow's trigger in words for the list row (#3683); the next tick of its schedule is <see cref="WorkflowNextRun"/>.</summary>
internal static class TriggerWords
{
    /// <summary>
    /// The trigger in words: 'By hand', 'Every 20 min', 'Every weekday 7:00', 'Every day 7:00'; a schedule it has no words for shows its cron text, any other trigger its node type.
    /// A clock time is worded in <paramref name="local"/>, the zone the next run is shown in, and the days move with it ('Sun–Thu 23:00' for 7:00 UTC weekdays in Pacific winter time);
    /// a cron in a zone this box cannot read keeps its own time and says the zone's name.
    /// </summary>
    /// <param name="node">The trigger node, or null.</param>
    /// <param name="local">The zone to word clock times in.</param>
    /// <param name="now">When the offsets are read (a zone's offset moves with daylight saving).</param>
    /// <returns>The words.</returns>
    internal static string Of(GraphNode? node, TimeZoneInfo local, DateTimeOffset now)
    {
        if (node is null)
        {
            return "";
        }

        if (string.Equals(node.Type, "manual", StringComparison.Ordinal))
        {
            return "By hand";
        }

        if (!string.Equals(node.Type, "schedule", StringComparison.Ordinal))
        {
            return node.Type;
        }

        var crons = WorkflowNextRun.Crons(node);
        return crons.Count switch
        {
            0 => "On a schedule",
            1 => Words(crons[0], node.Config["timezone"] is JsonValue zone && zone.TryGetValue<string>(out var id) ? id : null, local, now),
            _ => string.Create(CultureInfo.InvariantCulture, $"{crons.Count} schedules"),
        };
    }

    private static string Words(string cron, string? zoneId, TimeZoneInfo local, DateTimeOffset now)
    {
        var f = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (f.Length != 5)
        {
            return cron;
        }

        return Interval(f) ?? Daily(f, zoneId, local, now) ?? cron;
    }

    private static string? Interval(string[] f) =>
        f[0].StartsWith("*/", StringComparison.Ordinal) && int.TryParse(f[0][2..], NumberStyles.None, CultureInfo.InvariantCulture, out var every) && every > 0 && f[1] == "*" && f[2] == "*" && f[3] == "*" && f[4] == "*"
            ? string.Create(CultureInfo.InvariantCulture, $"Every {every} min")
            : null;

    private static string? Daily(string[] f, string? zoneId, TimeZoneInfo local, DateTimeOffset now)
    {
        if (!int.TryParse(f[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minute) || !int.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hour) || f[2] != "*" || f[3] != "*")
        {
            return null;
        }

        var weekdays = string.Equals(f[4], "1-5", StringComparison.Ordinal) || string.Equals(f[4], "MON-FRI", StringComparison.Ordinal);
        if (!weekdays && !string.Equals(f[4], "*", StringComparison.Ordinal))
        {
            return null;
        }

        return TryShift(zoneId, local, now, out var shift)
            ? InLocal(weekdays, (hour * 60) + minute + shift, (hour, minute), zoneId)
            : Label(weekdays) + " " + Time(hour, minute) + " " + zoneId;   // a zone this box cannot read: the cron's own time, named
    }

    // total is the clock time in minutes in the local zone, past midnight or before it when the zones' days differ; own is the time as the cron wrote it
    private static string InLocal(bool weekdays, int total, (int Hour, int Minute) own, string? zoneId)
    {
        var dayShift = (int)Math.Floor(total / 1440.0);
        var inDay = ((total % 1440) + 1440) % 1440;
        var time = Time(inDay / 60, inDay % 60);
        if (!weekdays || dayShift == 0)
        {
            return Label(weekdays) + " " + time;
        }

        var (first, last) = ((((1 + dayShift) % 7) + 7) % 7, (((5 + dayShift) % 7) + 7) % 7);   // Sunday is 0
        return first <= last
            ? _days[first] + "\u2013" + _days[last] + " " + time
            : "Every weekday " + Time(own.Hour, own.Minute) + " " + (zoneId ?? "UTC");   // a day range that wraps the week has no short words
    }

    // the minutes the local zone is ahead of the cron's zone at now; false when the cron's zone is not one this box knows
    private static bool TryShift(string? zoneId, TimeZoneInfo local, DateTimeOffset now, out int minutes)
    {
        minutes = 0;
        if (!TryZone(zoneId, out var from))
        {
            return false;
        }

        minutes = (int)(local.GetUtcOffset(now) - from.GetUtcOffset(now)).TotalMinutes;
        return true;
    }

    private static readonly string[] _days = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    private static string Label(bool weekdays) => weekdays ? "Every weekday" : "Every day";

    private static string Time(int hour, int minute) => string.Create(CultureInfo.InvariantCulture, $"{hour}:{minute:D2}");

    // why: a cron with no timezone is read in UTC (CronExpression.TryParse)
    private static bool TryZone(string? id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        zone = TimeZoneInfo.Utc;
        if (id is null)
        {
            return true;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = null;
            return false;
        }
    }
}
