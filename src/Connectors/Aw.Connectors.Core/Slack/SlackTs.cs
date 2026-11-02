using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Aw.Connectors.Core.Slack;

/// <summary>Slack timestamps: '1700000000.000100', seconds then six digits of microseconds. Pure helpers for ordering, days and permalinks.</summary>
public static class SlackTs
{
    private const long TicksPerMicrosecond = 10;
    private const long MaxSeconds = 253_402_300_799;

    /// <summary>Reads a ts.</summary>
    /// <param name="ts">The text.</param>
    /// <param name="value">The instant, to the microsecond.</param>
    /// <returns>Whether <paramref name="ts"/> is a ts.</returns>
    public static bool TryParse(string? ts, out DateTimeOffset value)
    {
        value = default;
        if (!HasShape(ts, out var dot)
            || !long.TryParse(ts.AsSpan(0, dot), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || !long.TryParse(ts.AsSpan(dot + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var micros)
            || seconds > MaxSeconds)
        {
            return false;
        }

        value = DateTimeOffset.UnixEpoch.AddSeconds(seconds).AddTicks(micros * TicksPerMicrosecond);
        return true;
    }

    private static bool HasShape([NotNullWhen(true)] string? ts, out int dot)
    {
        dot = ts?.IndexOf('.', StringComparison.Ordinal) ?? -1;
        return ts is not null
            && dot is >= 1 and <= 12
            && ts.Length - dot - 1 == 6
            && ts.LastIndexOf('.') == dot
            && ts.All(static c => c == '.' || char.IsAsciiDigit(c));
    }

    /// <summary>Formats an instant as a ts.</summary>
    /// <param name="value">The instant.</param>
    /// <returns>'seconds.micros'.</returns>
    public static string Format(DateTimeOffset value)
    {
        var micros = (value - DateTimeOffset.UnixEpoch).Ticks / TicksPerMicrosecond;
        return string.Create(CultureInfo.InvariantCulture, $"{micros / 1_000_000}.{micros % 1_000_000:D6}");
    }

    /// <summary>The ts of midnight (UTC) at the start of the day <paramref name="ts"/> falls on.</summary>
    /// <param name="ts">A ts.</param>
    /// <returns>The day start as a ts, or <paramref name="ts"/> itself when it does not parse.</returns>
    public static string DayStart(string ts)
        => TryParse(ts, out var at) ? Format(new DateTimeOffset(at.UtcDateTime.Date, TimeSpan.Zero)) : ts;

    /// <summary>The UTC day a ts falls on, 'yyyy-MM-dd'.</summary>
    /// <param name="at">The instant.</param>
    /// <returns>The day.</returns>
    public static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The later of two ts values (the first when one does not parse).</summary>
    /// <param name="a">A ts.</param>
    /// <param name="b">A ts.</param>
    /// <returns>The later.</returns>
    public static string Later(string a, string b)
        => TryParse(a, out var x) && TryParse(b, out var y) && y > x ? b : a;

    /// <summary>The ts one microsecond after <paramref name="ts"/> (the exclusive bound that includes it).</summary>
    /// <param name="ts">A ts.</param>
    /// <returns>The next ts, or <paramref name="ts"/> itself when it does not parse.</returns>
    public static string Next(string ts)
        => TryParse(ts, out var at) ? Format(at.AddTicks(TicksPerMicrosecond)) : ts;

    /// <summary>Whether <paramref name="a"/> is strictly earlier than <paramref name="b"/> (false when either does not parse).</summary>
    /// <param name="a">A ts.</param>
    /// <param name="b">A ts.</param>
    /// <returns>True when a is before b.</returns>
    public static bool IsBefore(string a, string b)
        => TryParse(a, out var x) && TryParse(b, out var y) && x < y;
}
