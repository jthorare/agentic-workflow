using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Resources;

/// <summary>Sizes in words: B, KB, MB or GB with one decimal ("1.2 MB").</summary>
internal static class ByteSize
{
    private static readonly string[] _units = ["B", "KB", "MB", "GB"];

    /// <summary>Words a size.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <param name="trimZero">True drops a trailing ".0" ("2 KB"); false keeps it ("5.0 MB"), as a limit reads.</param>
    /// <returns>The words.</returns>
    internal static string Format(long bytes, bool trimZero)
    {
        double value = Math.Max(bytes, 0);
        var unit = 0;
        while (value >= 1024 && unit < _units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var number = unit == 0 ? value.ToString("0", CultureInfo.InvariantCulture) : value.ToString("0.0", CultureInfo.InvariantCulture);
        if (trimZero && number.EndsWith(".0", StringComparison.Ordinal))
        {
            number = number[..^2];
        }

        return number + " " + _units[unit];
    }
}
