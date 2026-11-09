using System.Globalization;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>A cursor that is a timestamp: the newest one seen, kept as the provider wrote it.</summary>
/// <param name="Text">The timestamp as the provider wrote it, or null.</param>
/// <param name="At">The same instant, or null.</param>
internal readonly record struct PollCursorText(string? Text, DateTimeOffset? At)
{
    /// <summary>Keeps the later of this and a candidate.</summary>
    /// <param name="candidate">A timestamp as text.</param>
    /// <returns>The later one.</returns>
    internal PollCursorText Later(string? candidate)
        => PollJson.Time(candidate) is { } time && (At is null || time > At) ? new PollCursorText(candidate, time) : this;

    /// <summary>A baseline for a feed that has nothing: now, in the timestamp form the pollers write.</summary>
    /// <param name="now">The time.</param>
    /// <returns>The cursor text.</returns>
    internal static string Now(DateTimeOffset now) => now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
