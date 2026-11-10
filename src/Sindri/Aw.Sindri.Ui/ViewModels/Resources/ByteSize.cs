using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Resources;

/// <summary>Sizes in words: B, KB, MB or GB with one decimal and no trailing ".0" ("1.2 MB", "5 MB").</summary>
internal static class ByteSize
{
    private static readonly string[] _units = ["B", "KB", "MB", "GB"];

    /// <summary>Words a size.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The words.</returns>
    internal static string Format(long bytes)
    {
        double value = Math.Max(bytes, 0);
        var unit = 0;
        while (Math.Round(value, 1, MidpointRounding.AwayFromZero) >= 1024 && unit < _units.Length - 1)   // rounds first: 1_048_575 is "1 MB", not "1024 KB"
        {
            value /= 1024;
            unit++;
        }

        var number = unit == 0 ? value.ToString("0", CultureInfo.InvariantCulture) : value.ToString("0.0", CultureInfo.InvariantCulture);
        if (number.EndsWith(".0", StringComparison.Ordinal))
        {
            number = number[..^2];
        }

        return number + " " + _units[unit];
    }
}
