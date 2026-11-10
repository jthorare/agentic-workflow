using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// Reads the instant out of the strip's marker, only to say when the newest change you saw happened. The route gives the marker as 'lin_' and 16 hex digits of UTC ticks;
/// the app passes it back unchanged and never builds one, and a marker that doesn't read this way just has no time to say.
/// </summary>
internal static class LinearSinceMarker
{
    private const string Prefix = "lin_";

    /// <summary>Reads a marker's instant.</summary>
    /// <param name="marker">The stored marker; may be null.</param>
    /// <param name="at">The instant, when it reads.</param>
    /// <returns>True when it reads.</returns>
    internal static bool TryRead(string? marker, out DateTimeOffset at)
    {
        at = default;
        if (marker is null || marker.Length != Prefix.Length + 16 || !marker.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!long.TryParse(marker.AsSpan(Prefix.Length), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var ticks) || ticks < 0 || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return false;
        }

        at = new DateTimeOffset(ticks, TimeSpan.Zero);
        return true;
    }
}
