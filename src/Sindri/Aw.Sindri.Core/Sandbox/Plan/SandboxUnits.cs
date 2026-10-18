using System.Globalization;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Parses and formats the sizes, percentages and durations of the profile's sandbox section (already checked by the profile rules).</summary>
public static class SandboxUnits
{
    private const long Kib = 1024;
    private const long Mib = Kib * 1024;
    private const long Gib = Mib * 1024;
    private const long Tib = Gib * 1024;

    /// <summary>Parses a size such as '256MiB' or '12GiB'.</summary>
    /// <param name="size">The size text.</param>
    /// <returns>Bytes.</returns>
    public static long ParseSize(string size)
    {
        ArgumentNullException.ThrowIfNull(size);
        var unit = size[^3..];
        var multiplier = unit switch
        {
            "KiB" => Kib,
            "MiB" => Mib,
            "GiB" => Gib,
            "TiB" => Tib,
            _ => throw new ArgumentException($"'{size}' is not a size such as 256MiB", nameof(size)),
        };
        return long.Parse(size.AsSpan(0, size.Length - 3), NumberStyles.None, CultureInfo.InvariantCulture) * multiplier;
    }

    /// <summary>Formats bytes the way the summary shows them: whole GiB or MiB when exact, else GiB with one decimal.</summary>
    /// <param name="bytes">The size.</param>
    /// <returns>For example '12GiB'.</returns>
    public static string FormatSize(long bytes) => bytes switch
    {
        > 0 when bytes % Gib == 0 => string.Create(CultureInfo.InvariantCulture, $"{bytes / Gib}GiB"),
        > 0 when bytes % Mib == 0 => string.Create(CultureInfo.InvariantCulture, $"{bytes / Mib}MiB"),
        >= Gib => string.Create(CultureInfo.InvariantCulture, $"{(double)bytes / Gib:0.0}GiB"),
        >= Mib => string.Create(CultureInfo.InvariantCulture, $"{bytes / Mib}MiB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / Kib}KiB"),
    };

    /// <summary>Parses '50%' to 50.</summary>
    /// <param name="percent">The percent text.</param>
    /// <returns>The number.</returns>
    public static int ParsePercent(string percent)
    {
        ArgumentNullException.ThrowIfNull(percent);
        return int.Parse(percent.AsSpan(0, percent.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture);
    }

    /// <summary>Formats a duration as whole hours ('4h') or minutes ('70m').</summary>
    /// <param name="duration">The duration.</param>
    /// <returns>The text.</returns>
    public static string FormatDuration(TimeSpan duration)
    {
        var minutes = (long)duration.TotalMinutes;
        return minutes > 0 && minutes % 60 == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{minutes / 60}h")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}m");
    }
}
