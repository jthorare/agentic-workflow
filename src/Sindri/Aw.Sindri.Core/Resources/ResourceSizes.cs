using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Resources;

/// <summary>Parses the sizes a resource declaration uses: '512MiB', '4GiB', or the short forms '512m', '4g'.</summary>
public static partial class ResourceSizes
{
    /// <summary>Parses a size.</summary>
    /// <param name="text">The size as written.</param>
    /// <param name="bytes">The size in bytes when it parses.</param>
    /// <returns>True when <paramref name="text"/> is a size that fits in a long.</returns>
    public static bool TryParse(string? text, out long bytes)
    {
        bytes = 0;
        if (text is null || Shape.Match(text) is not { Success: true } match)
        {
            return false;
        }

        var unit = match.Groups["unit"].Value switch
        {
            "KiB" or "k" => 1L << 10,
            "MiB" or "m" => 1L << 20,
            "GiB" or "g" => 1L << 30,
            _ => 1L << 40,
        };
        var number = long.Parse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture);
        if (number > long.MaxValue / unit)
        {
            return false;   // over ~8.4 million TiB: refused, never a wrapped negative size
        }

        bytes = number * unit;
        return true;
    }

    /// <summary>Spells a size with the long unit: '4g' becomes '4GiB', '512MiB' stays '512MiB'.</summary>
    /// <param name="text">The size as written.</param>
    /// <param name="canonical">The size with a KiB, MiB, GiB or TiB unit when it parses.</param>
    /// <returns>True when <paramref name="text"/> is a size <see cref="TryParse"/> accepts.</returns>
    public static bool TryNormalize(string? text, [NotNullWhen(true)] out string? canonical)
    {
        canonical = null;
        if (text is null || !TryParse(text, out _) || Shape.Match(text) is not { Success: true } match)
        {
            return false;
        }

        var unit = match.Groups["unit"].Value switch
        {
            "k" => "KiB",
            "m" => "MiB",
            "g" => "GiB",
            "t" => "TiB",
            var full => full,
        };
        canonical = match.Groups["number"].Value + unit;
        return true;
    }

    [GeneratedRegex("^(?<number>[1-9][0-9]{0,6})(?<unit>KiB|MiB|GiB|TiB|k|m|g|t)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
