using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Core.Graph.Schedule;

/// <summary>
/// A five-field cron schedule (minute hour day-of-month month day-of-week) read in an IANA timezone. A day-of-month and a day-of-week that are both
/// restricted match on either (the Vixie rule). A local time a spring-forward skips fires once, at the first valid instant after it; a local time a
/// fall-back repeats fires once, at its first occurrence.
/// </summary>
public sealed class CronExpression
{
    private static readonly string[] _monthNames = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];
    private static readonly string[] _dayNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];
    private static readonly int[] _monthLengths = [31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

    private static readonly CronFieldSpec[] _specs =
    [
        new(Name: "minute", Min: 0, Max: 59, Names: null, NameBase: 0),
        new(Name: "hour", Min: 0, Max: 23, Names: null, NameBase: 0),
        new(Name: "day-of-month", Min: 1, Max: 31, Names: null, NameBase: 0),
        new(Name: "month", Min: 1, Max: 12, Names: _monthNames, NameBase: 1),
        new(Name: "day-of-week", Min: 0, Max: 7, Names: _dayNames, NameBase: 0),
    ];

    private readonly bool[] _minutes;
    private readonly bool[] _hours;
    private readonly bool[] _daysOfMonth;
    private readonly bool[] _months;
    private readonly bool[] _daysOfWeek;
    private readonly bool _eitherDay;
    private readonly TimeZoneInfo _zone;

    private CronExpression(bool[] minutes, bool[] hours, bool[] daysOfMonth, bool[] months, bool[] daysOfWeek, bool eitherDay, TimeZoneInfo zone)
    {
        _minutes = minutes;
        _hours = hours;
        _daysOfMonth = daysOfMonth;
        _months = months;
        _daysOfWeek = daysOfWeek;
        _eitherDay = eitherDay;
        _zone = zone;
    }

    /// <summary>Reads <paramref name="cron"/> in <paramref name="timezone"/> (null is UTC).</summary>
    /// <param name="cron">Five fields: <c language="text">*</c>, lists, ranges, steps, 0-7 for Sunday, MON and JAN names.</param>
    /// <param name="timezone">An IANA id such as America/Los_Angeles, or null for UTC.</param>
    /// <param name="expression">The schedule; null when the text is not valid.</param>
    /// <param name="error">Which field or timezone is wrong and the fix; null when valid.</param>
    /// <returns>True when the schedule reads.</returns>
    public static bool TryParse(string cron, string? timezone, [NotNullWhen(true)] out CronExpression? expression, [NotNullWhen(false)] out string? error)
    {
        ArgumentNullException.ThrowIfNull(cron);
        expression = null;
        var zone = TimeZoneInfo.Utc;
        if (timezone is not null && !TryFindZone(timezone, out zone))
        {
            error = $"timezone '{timezone}' is not a known IANA id; use a name like America/Los_Angeles, or leave the timezone out for UTC";
            return false;
        }

        var fields = cron.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 5)
        {
            error = "a cron needs 5 fields (minute hour day-of-month month day-of-week), got " + CronFieldSpec.Num(fields.Length);
            return false;
        }

        bool[][] sets = [new bool[60], new bool[24], new bool[32], new bool[13], new bool[8]];
        for (var i = 0; i < 5; i++)
        {
            if (!_specs[i].TryFill(fields[i], sets[i], out error))
            {
                return false;
            }
        }

        sets[4][0] |= sets[4][7];
        var domRestricted = fields[2][0] != '*';
        var dowRestricted = fields[4][0] != '*';
        if (domRestricted && !dowRestricted && !DayExists(sets[2], sets[3]))
        {
            error = "day-of-month never occurs in the chosen month (for example 30 in February); use a day the month has";
            return false;
        }

        expression = new CronExpression(sets[0], sets[1], sets[2], sets[3], sets[4], domRestricted && dowRestricted, zone);
        error = null;
        return true;
    }

    /// <summary>The first tick strictly after <paramref name="after"/>.</summary>
    /// <param name="after">The instant to start after.</param>
    /// <returns>The tick, as an instant.</returns>
    public DateTimeOffset Next(DateTimeOffset after)
    {
        var local = TimeZoneInfo.ConvertTime(after, _zone).DateTime;
        var start = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, DateTimeKind.Unspecified).AddMinutes(1);
        while (true)
        {
            var wall = NextWall(start);
            var instant = ToInstant(wall);
            if (instant > after)
            {
                return TimeZoneInfo.ConvertTime(instant, _zone);
            }

            // why: a repeated local time maps to its first occurrence, which can already be past.
            start = wall.AddMinutes(1);
        }
    }

    /// <summary>The latest tick <c language="text">t</c> with <paramref name="after"/> &lt; t &lt;= <paramref name="upTo"/> (a missed schedule runs once).</summary>
    /// <param name="after">The last instant already handled.</param>
    /// <param name="upTo">The newest instant to consider, included.</param>
    /// <returns>The tick, or null when none fell in the window.</returns>
    public DateTimeOffset? LastTick(DateTimeOffset after, DateTimeOffset upTo)
    {
        DateTimeOffset? last = null;
        var cursor = after;
        while (cursor < upTo)
        {
            var next = Next(cursor);
            if (next > upTo)
            {
                break;
            }

            last = next;
            cursor = next;
        }

        return last;
    }

    private static bool TryFindZone(string id, [NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        zone = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static bool DayExists(bool[] days, bool[] months)
    {
        for (var month = 1; month <= 12; month++)
        {
            for (var day = 1; day <= _monthLengths[month - 1]; day++)
            {
                if (months[month] && days[day])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool DayMatches(DateTime t)
    {
        var dom = _daysOfMonth[t.Day];
        var dow = _daysOfWeek[(int)t.DayOfWeek];
        return _eitherDay ? dom || dow : dom && dow;
    }

    private DateTime NextWall(DateTime start)
    {
        var t = start;
        var limit = start.Year + 9;
        while (t.Year <= limit)
        {
            if (!_months[t.Month])
            {
                t = new DateTime(t.Year, t.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(1);
            }
            else if (!DayMatches(t))
            {
                t = t.Date.AddDays(1);
            }
            else if (!_hours[t.Hour])
            {
                t = t.Date.AddHours(t.Hour + 1);
            }
            else if (!_minutes[t.Minute])
            {
                t = t.AddMinutes(1);
            }
            else
            {
                return t;
            }
        }

        throw new InvalidOperationException("the schedule has no tick in the next nine years");
    }

    private DateTimeOffset ToInstant(DateTime wall)
    {
        // why: a skipped local time fires at the first valid wall time after the gap; a repeated one at its first (larger-offset) occurrence.
        while (_zone.IsInvalidTime(wall))
        {
            wall = wall.AddMinutes(1);
        }

        var offset = _zone.IsAmbiguousTime(wall) ? _zone.GetAmbiguousTimeOffsets(wall).Max() : _zone.GetUtcOffset(wall);
        return new DateTimeOffset(wall, offset);
    }
}
