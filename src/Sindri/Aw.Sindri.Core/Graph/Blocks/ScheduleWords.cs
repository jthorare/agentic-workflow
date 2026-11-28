using System.Globalization;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>A daily or weekly cron in the Schedule presets' words (#4254): "every Thursday at 7:00", "every Mon, Wed and Fri at 18:30" (short names past one day, so the card keeps the time, #4329), "every day but Sunday at 7:00", "every weekday at 6:05", "every day at 7:00".</summary>
internal static class ScheduleWords
{
    private static readonly string[] _dayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
    private static readonly string[] _cronNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

    // why: weeks read Monday first, so Sunday (cron 0) goes last
    private static readonly int[] _weekOrder = [1, 2, 3, 4, 5, 6, 0];

    /// <summary>The words for a cron that fires once a day at one fixed time on a set of weekdays; null for any other shape (steps, hour or minute lists, a day of month or month).</summary>
    /// <param name="cron">The cron text, five fields.</param>
    /// <returns>The words, or null.</returns>
    internal static string? Of(string cron)
    {
        if (!Parse(cron, out var minute, out var hour, out var days))
        {
            return null;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{DayWords(days)} at {hour}:{minute:D2}");
    }

    // the shape the words cover: once a day at one time, on a set of weekdays
    private static bool Parse(string cron, out int minute, out int hour, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out bool[]? days)
    {
        (minute, hour, days) = (0, 0, null);
        var f = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (f.Length != 5 || f[2] != "*" || f[3] != "*"
            || !int.TryParse(f[0], NumberStyles.None, CultureInfo.InvariantCulture, out minute) || minute > 59
            || !int.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out hour) || hour > 23)
        {
            return false;
        }

        days = Days(f[4]);
        return days is not null;
    }

    /// <summary>The same words in <paramref name="local"/>: the clock time moves by the zones' difference at <paramref name="now"/> and the days move with it ("Sun–Thu at 23:00" for 7:00 UTC weekdays in Pacific winter time).</summary>
    /// <param name="cron">The cron text, five fields.</param>
    /// <param name="zoneId">The cron's IANA zone, or null for UTC.</param>
    /// <param name="local">The zone to word the time in.</param>
    /// <param name="now">When the offsets are read.</param>
    /// <returns>The words; null when the cron has another shape, its zone is unknown, or the two zones agree (the plain words then stand, with the zone's name).</returns>
    internal static string? Local(string cron, string? zoneId, TimeZoneInfo local, DateTimeOffset now)
    {
        if (!Parse(cron, out var minute, out var hour, out var days) || !TryZone(zoneId, out var from))
        {
            return null;
        }

        var shift = (int)(local.GetUtcOffset(now) - from.GetUtcOffset(now)).TotalMinutes;
        if (shift == 0)
        {
            return null;
        }

        var total = (hour * 60) + minute + shift;
        var dayShift = (int)Math.Floor(total / 1440.0);
        var inDay = ((total % 1440) + 1440) % 1440;
        var moved = new bool[7];
        for (var d = 0; d < 7; d++)
        {
            moved[(((d + dayShift) % 7) + 7) % 7] = days[d];
        }

        return string.Create(CultureInfo.InvariantCulture, $"{DayWords(moved, ranges: true)} at {inDay / 60}:{inDay % 60:D2}");
    }

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

    private static string DayWords(bool[] days, bool ranges = false)
    {
        var picked = _weekOrder.Where(d => days[d]).ToList();
        if (picked.Count == 7)
        {
            return "every day";
        }

        if (picked.Count == 5 && !days[0] && !days[6])
        {
            return "every weekday";
        }

        if (picked.Count == 6)
        {
            return "every day but " + _dayNames[Array.IndexOf(days, value: false)];
        }

        var order = Enumerable.Range(0, 7).Where(d => days[d]).ToList();
        if (ranges && order.Count >= 3 && order[^1] - order[0] == order.Count - 1)
        {
            return _dayNames[order[0]][..3] + "\u2013" + _dayNames[order[^1]][..3];   // a run of days from Sunday to Saturday reads as a range
        }

        // why: the canvas card holds about 44 characters (#4329); full names for two or more days push the time off it
        var names = picked.Select(d => _dayNames[d][..3]).ToList();
        return picked.Count == 1 ? "every " + _dayNames[picked[0]] : $"every {string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
    }

    // why: index 0 is Sunday; cron's 7 is Sunday too
    private static bool[]? Days(string field)
    {
        var days = new bool[7];
        if (field == "*")
        {
            Array.Fill(days, value: true);
            return days;
        }

        foreach (var item in field.Split(','))
        {
            var ends = item.Split('-');
            if (ends.Length > 2 || Day(ends[0]) is not { } from || Day(ends[^1]) is not { } to || from > to)
            {
                return null;
            }

            for (var d = from; d <= to; d++)
            {
                days[d % 7] = true;
            }
        }

        return days;
    }

    private static int? Day(string text)
    {
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return number <= 7 ? number : null;
        }

        var named = Array.FindIndex(_cronNames, n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
        return named >= 0 ? named : null;
    }
}
