using System.Buffers;
using System.Globalization;

namespace Aw.Sindri.Features.GetLinearSince;

/// <summary>The strip's marker on the wire: 'lin_' and the 16 lowercase hex digits of an instant's UTC ticks.</summary>
internal static class LinearSinceMarkers
{
    private const string Prefix = "lin_";
    private const int Digits = 16;
    private static readonly SearchValues<char> _hex = SearchValues.Create("0123456789abcdef");

    /// <summary>The marker of an instant.</summary>
    /// <param name="at">The instant.</param>
    /// <returns>The marker.</returns>
    internal static string Format(DateTimeOffset at) => Prefix + at.UtcTicks.ToString("x16", CultureInfo.InvariantCulture);

    /// <summary>Reads a marker back.</summary>
    /// <param name="text">The text.</param>
    /// <param name="at">The instant, when the text is a marker.</param>
    /// <returns>Whether the text is a marker.</returns>
    internal static bool TryParse(string text, out DateTimeOffset at)
    {
        at = default;
        if (text.Length != Prefix.Length + Digits || !text.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var hex = text.AsSpan(Prefix.Length);
        if (hex.ContainsAnyExcept(_hex) || !long.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var ticks) || ticks < 0 || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return false;
        }

        at = new DateTimeOffset(ticks, TimeSpan.Zero);
        return true;
    }
}
