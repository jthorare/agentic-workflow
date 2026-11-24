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
        var f = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (f.Length != 5 || f[2] != "*" || f[3] != "*"
            || !int.TryParse(f[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minute) || minute > 59
            || !int.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hour) || hour > 23
            || Days(f[4]) is not { } days)
        {
            return null;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{DayWords(days)} at {hour}:{minute:D2}");
    }

    private static string DayWords(bool[] days)
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
