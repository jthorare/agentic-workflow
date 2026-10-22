using System.Globalization;

namespace Aw.Sindri.Core.Sessions;

/// <summary>A process's start time as macOS ps prints it ('Wed Oct 21 16:22:11 2026', read with TZ=UTC), as epoch seconds: the start ticks the macOS table uses, and the form Claude stores as a record's procStart there.</summary>
public static class PsStartTime
{
    private const int Parts = 5;
    private const int WeekdayLength = 3;

    /// <summary>Reads ps's lstart text.</summary>
    /// <param name="text">The text: weekday, month, day, time and year.</param>
    /// <returns>Seconds since the Unix epoch (the text is UTC), or null when it isn't an lstart text.</returns>
    public static long? ToEpochSeconds(string? text)
    {
        var parts = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts is { Length: Parts } && parts[0].Length == WeekdayLength
            && DateTimeOffset.TryParseExact(string.Join(' ', parts[1..]), "MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
                ? at.ToUnixTimeSeconds()
                : null;
    }
}
